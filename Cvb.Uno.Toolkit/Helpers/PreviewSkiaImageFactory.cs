using CommonVisionNodes.Contracts;
using SkiaSharp;

namespace Cvb.Uno.Toolkit.Helpers;

/// <summary>
/// Creates owned Skia images from raw preview frames.
/// </summary>
public static class PreviewSkiaImageFactory
{
	/// <summary>
	/// Copies a raw preview into an immutable Skia image that is safe to retain after transport acknowledgement.
	/// </summary>
	/// <param name="preview">Raw preview metadata.</param>
	/// <param name="source">Raw preview bytes.</param>
	/// <param name="expandedBgraBuffer">Reusable BGRA buffer used only for RGB24 input.</param>
	/// <returns>An owned Skia image.</returns>
	public static SKImage Create(
		ImagePreviewDto preview,
		byte[] source,
		ref byte[]? expandedBgraBuffer)
	{
		ArgumentNullException.ThrowIfNull(preview);
		ArgumentNullException.ThrowIfNull(source);

		var width = Math.Max(1, preview.PreviewWidth);
		var height = Math.Max(1, preview.PreviewHeight);

		return preview.Encoding switch
		{
			ImagePreviewEncodingDto.Gray8 => CreateCopy(
				preview,
				source,
				new SKImageInfo(width, height, SKColorType.Gray8, SKAlphaType.Opaque),
				1),
			ImagePreviewEncodingDto.Bgra32 => CreateCopy(
				preview,
				source,
				new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Unpremul),
				4),
			ImagePreviewEncodingDto.Rgb24 => CreateRgb24Copy(preview, source, ref expandedBgraBuffer),
			_ => throw new ArgumentException("The preview is not a supported raw pixel format.", nameof(preview))
		};
	}

	private static SKImage CreateRgb24Copy(
		ImagePreviewDto preview,
		byte[] source,
		ref byte[]? expandedBgraBuffer)
	{
		var requiredBytes = PreviewPixelBufferWriter.GetBgra32ByteCount(preview);
		if (expandedBgraBuffer?.Length != requiredBytes)
			expandedBgraBuffer = GC.AllocateUninitializedArray<byte>(requiredBytes);

		PreviewPixelBufferWriter.ExpandToBgra32(preview, source, expandedBgraBuffer);

		var info = new SKImageInfo(
			Math.Max(1, preview.PreviewWidth),
			Math.Max(1, preview.PreviewHeight),
			SKColorType.Bgra8888,
			SKAlphaType.Opaque);
		return SKImage.FromPixelCopy(info, expandedBgraBuffer, info.RowBytes)
			?? throw new InvalidOperationException("Skia could not create an RGB24 preview image.");
	}

	private static SKImage CreateCopy(
		ImagePreviewDto preview,
		byte[] source,
		SKImageInfo info,
		int bytesPerPixel)
	{
		var minimumStride = checked(info.Width * bytesPerPixel);
		var sourceStride = preview.Stride > 0 ? preview.Stride : minimumStride;
		if (sourceStride < minimumStride)
			throw new InvalidDataException("Raw preview stride is smaller than its pixel width.");

		var requiredBytes = checked(sourceStride * info.Height);
		if (source.Length < requiredBytes)
			throw new InvalidDataException("Raw preview payload is smaller than the declared dimensions.");

		return SKImage.FromPixelCopy(info, source, sourceStride)
			?? throw new InvalidOperationException("Skia could not create a raw preview image.");
	}
}
