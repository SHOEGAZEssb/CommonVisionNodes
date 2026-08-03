using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.WindowsRuntime;
using CommonVisionNodes.Contracts;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage.Streams;

namespace Cvb.Uno.Toolkit.Helpers;

/// <summary>
/// Loads CVB image preview payloads into Uno image controls.
/// </summary>
public static class PreviewImageSourceLoader
{
    private static readonly ConditionalWeakTable<Image, ImageLoadState> LoadStates = [];

    /// <summary>
    /// Decodes a preview payload and assigns it to the provided image control.
    /// </summary>
    /// <param name="image">Image control to update.</param>
    /// <param name="preview">Preview payload, or <c>null</c> to clear the source.</param>
    /// <returns>The preview that was applied, or <c>null</c> when a newer preview superseded this call.</returns>
    public static async Task<ImagePreviewDto?> SetImageAsync(Image image, ImagePreviewDto? preview)
    {
        var state = LoadStates.GetOrCreateValue(image);

        if (preview is null || !HasImageData(preview))
        {
            ClearImage(image);
            return null;
        }

        // Raw previews are the hot path and require no decode. Applying them before returning keeps
        // transport-buffer reuse safe: the WebSocket listener cannot start receiving the next frame
        // until this pixel-buffer copy has completed on the UI thread.
        if (ImagePreviewEncodingInfo.IsRaw(preview.Encoding) && preview.BinaryData is { Length: > 0 } binaryData)
            return ApplyRawPreview(image, state, preview, binaryData);

        lock (state.Sync)
        {
            state.Version++;
            state.PendingPreview = preview;
            state.HasPendingPreview = true;

            if (state.IsProcessing)
                return null;

            state.IsProcessing = true;
        }

        return await ProcessLatestPreviewAsync(image, state);
    }

    /// <summary>
    /// Formats source and preview dimensions for display in image overlays.
    /// </summary>
    /// <param name="preview">Preview payload to describe.</param>
    /// <returns>Human-readable image information.</returns>
    public static string GetPreviewInfoText(ImagePreviewDto preview)
    {
        var sourceSize = $"{preview.Width} x {preview.Height}";
        if (preview.PreviewWidth > 0 &&
            preview.PreviewHeight > 0 &&
            (preview.PreviewWidth != preview.Width || preview.PreviewHeight != preview.Height))
        {
            return $"{sourceSize} -> preview {preview.PreviewWidth} x {preview.PreviewHeight}  {preview.PixelFormat}";
        }

        return $"{sourceSize}  {preview.PixelFormat}";
    }

    /// <summary>
    /// Clears an image control and invalidates any in-flight preview decode for it.
    /// </summary>
    /// <param name="image">Image control to clear.</param>
    public static void ClearImage(Image image)
    {
        var state = LoadStates.GetOrCreateValue(image);
        lock (state.Sync)
        {
            state.Version++;
            state.PendingPreview = null;
            state.HasPendingPreview = true;
            state.BgraBitmap = null;
            state.BgraWidth = 0;
            state.BgraHeight = 0;
            state.ExpandedBgraBuffer = null;
        }

        image.Source = null;
    }

    private static async Task<ImagePreviewDto?> ProcessLatestPreviewAsync(Image image, ImageLoadState state)
    {
        ImagePreviewDto? appliedPreview = null;

        while (true)
        {
            ImagePreviewDto? preview;
            int version;

            lock (state.Sync)
            {
                if (!state.HasPendingPreview)
                {
                    state.IsProcessing = false;
                    return appliedPreview;
                }

                preview = state.PendingPreview;
                version = state.Version;
                state.HasPendingPreview = false;
            }

            if (preview is null || !HasImageData(preview))
            {
                image.Source = null;
                appliedPreview = null;
                continue;
            }

            var bytes = await GetPreviewBytesAsync(preview);
            if (HasNewerPreview(state, version))
                continue;

            // A control can receive newer previews while an older payload is decoding.
            // Only one worker per control is allowed, and it always jumps to the newest frame.
            var source = ImagePreviewEncodingInfo.IsRaw(preview.Encoding)
                ? CreateRawBitmap(state, preview, bytes)
                : await CreateEncodedBitmapAsync(bytes);

            if (HasNewerPreview(state, version))
                continue;

            if (!ReferenceEquals(image.Source, source))
                image.Source = source;

            appliedPreview = preview;
        }
    }

    private static bool HasNewerPreview(ImageLoadState state, int version)
    {
        lock (state.Sync)
            return state.Version != version;
    }

    private static ImagePreviewDto ApplyRawPreview(
        Image image,
        ImageLoadState state,
        ImagePreviewDto preview,
        byte[] bytes)
    {
        lock (state.Sync)
        {
            state.Version++;
            state.PendingPreview = null;
            state.HasPendingPreview = false;
        }

        var source = CreateRawBitmap(state, preview, bytes);
        if (!ReferenceEquals(image.Source, source))
            image.Source = source;

        return preview;
    }

    private static bool HasImageData(ImagePreviewDto preview)
        => (preview.BinaryData is { Length: > 0 }) || !string.IsNullOrWhiteSpace(preview.Base64Data);

    private static Task<byte[]> GetPreviewBytesAsync(ImagePreviewDto preview)
    {
        if (preview.BinaryData is { Length: > 0 } binaryData)
            return Task.FromResult(binaryData);

        return Task.Run(() => Convert.FromBase64String(preview.Base64Data));
    }

    private static async Task<ImageSource> CreateEncodedBitmapAsync(byte[] bytes)
    {
        using var stream = new InMemoryRandomAccessStream();
        await stream.WriteAsync(bytes.AsBuffer());
        stream.Seek(0);

        var bitmap = new BitmapImage();
        await bitmap.SetSourceAsync(stream);
        return bitmap;
    }

    private static ImageSource CreateRawBitmap(ImageLoadState state, ImagePreviewDto preview, byte[] bytes)
    {
        var width = Math.Max(1, preview.PreviewWidth);
        var height = Math.Max(1, preview.PreviewHeight);

        var bitmap = GetOrCreateBgraBitmap(state, width, height);
        using var pixelStream = bitmap.PixelBuffer.AsStream();
        var expandedBytes = preview.Encoding == ImagePreviewEncodingDto.Bgra32
            ? null
            : GetOrCreateExpandedBgraBuffer(state, preview);
        PreviewPixelBufferWriter.WriteRawPreview(pixelStream, preview, bytes, expandedBytes);

        bitmap.Invalidate();
        return bitmap;
    }

    private static WriteableBitmap GetOrCreateBgraBitmap(ImageLoadState state, int width, int height)
    {
        lock (state.Sync)
        {
            if (state.BgraBitmap is not null &&
                state.BgraWidth == width &&
                state.BgraHeight == height)
            {
                return state.BgraBitmap;
            }

            state.BgraBitmap = new WriteableBitmap(width, height);
            state.BgraWidth = width;
            state.BgraHeight = height;
            state.ExpandedBgraBuffer = null;
            return state.BgraBitmap;
        }
    }

    private static byte[] GetOrCreateExpandedBgraBuffer(ImageLoadState state, ImagePreviewDto preview)
    {
        var byteCount = PreviewPixelBufferWriter.GetBgra32ByteCount(preview);
        lock (state.Sync)
        {
            if (state.ExpandedBgraBuffer?.Length != byteCount)
                state.ExpandedBgraBuffer = GC.AllocateUninitializedArray<byte>(byteCount);

            return state.ExpandedBgraBuffer;
        }
    }

    private sealed class ImageLoadState
    {
        public object Sync { get; } = new();

        public int Version;

        public bool IsProcessing;

        public bool HasPendingPreview;

        public ImagePreviewDto? PendingPreview;

        public WriteableBitmap? BgraBitmap;

        public int BgraWidth;

        public int BgraHeight;

        public byte[]? ExpandedBgraBuffer;
    }
}
