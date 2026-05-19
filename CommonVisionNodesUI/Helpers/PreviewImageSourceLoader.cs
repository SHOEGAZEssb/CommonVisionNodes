using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Runtime.CompilerServices;
using CommonVisionNodes.Contracts;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage.Streams;

namespace CommonVisionNodesUI.Helpers;

internal static class PreviewImageSourceLoader
{
    private static readonly ConditionalWeakTable<Image, ImageLoadState> LoadStates = new();

    public static async Task<bool> SetImageAsync(Image image, ImagePreviewDto? preview)
    {
        var state = LoadStates.GetOrCreateValue(image);
        var version = Interlocked.Increment(ref state.Version);

        if (preview is null || string.IsNullOrWhiteSpace(preview.Base64Data))
        {
            image.Source = null;
            return true;
        }

        var bytes = await Task.Run(() => Convert.FromBase64String(preview.Base64Data));
        if (Volatile.Read(ref state.Version) != version)
            return false;

        // Image controls can receive newer previews while an older payload is decoding. The
        // per-control version check prevents stale async work from replacing the latest frame.
        var source = preview.Encoding == ImagePreviewEncodingDto.Bgra32
            ? await CreateBgra32BitmapAsync(preview, bytes)
            : await CreateEncodedBitmapAsync(bytes);

        if (Volatile.Read(ref state.Version) != version)
            return false;

        image.Source = source;
        return true;
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

    private static async Task<ImageSource> CreateBgra32BitmapAsync(ImagePreviewDto preview, byte[] bytes)
    {
        var width = Math.Max(1, preview.PreviewWidth);
        var height = Math.Max(1, preview.PreviewHeight);
        var stride = preview.Stride > 0 ? preview.Stride : width * 4;
        var expectedByteCount = checked(stride * height);

        if (bytes.Length < expectedByteCount)
            throw new InvalidDataException("BGRA preview payload is smaller than the declared dimensions.");

        var bitmap = new WriteableBitmap(width, height);
        using var pixelStream = bitmap.PixelBuffer.AsStream();
        await pixelStream.WriteAsync(bytes, 0, expectedByteCount);
        bitmap.Invalidate();
        return bitmap;
    }

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

    private sealed class ImageLoadState
    {
        public int Version;
    }
}
