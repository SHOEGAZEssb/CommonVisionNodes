using CommonVisionNodes.Contracts;

namespace Cvb.Uno.Toolkit.Helpers;

/// <summary>
/// Converts raw preview data into a frontend BGRA bitmap pixel buffer.
/// </summary>
public static class PreviewPixelBufferWriter
{
	/// <summary>
	/// Writes any supported raw preview into Uno's BGRA32 pixel buffer.
	/// </summary>
	/// <param name="pixelBuffer">Writable bitmap pixel buffer.</param>
	/// <param name="preview">Preview metadata describing the frame.</param>
	/// <param name="source">Raw preview payload.</param>
	/// <param name="expandedBgraBuffer">Reusable BGRA buffer required for Gray8 and RGB24 sources.</param>
	public static void WriteRawPreview(
		Stream pixelBuffer,
		ImagePreviewDto preview,
		byte[] source,
		byte[]? expandedBgraBuffer = null)
	{
		ArgumentNullException.ThrowIfNull(pixelBuffer);
		ArgumentNullException.ThrowIfNull(preview);
		ArgumentNullException.ThrowIfNull(source);

		if (preview.Encoding == ImagePreviewEncodingDto.Bgra32)
		{
			WriteBgra32(pixelBuffer, preview, source);
			return;
		}

		if (preview.Encoding is not (ImagePreviewEncodingDto.Gray8 or ImagePreviewEncodingDto.Rgb24))
			throw new ArgumentException("The preview is not a supported raw pixel format.", nameof(preview));
		ArgumentNullException.ThrowIfNull(expandedBgraBuffer);

		ExpandToBgra32(preview, source, expandedBgraBuffer);
		pixelBuffer.Position = 0;
		pixelBuffer.Write(expandedBgraBuffer, 0, GetBgra32ByteCount(preview));
	}

	/// <summary>
	/// Gets the byte count required by Uno's tightly packed BGRA32 destination bitmap.
	/// </summary>
	public static int GetBgra32ByteCount(ImagePreviewDto preview)
	{
		ArgumentNullException.ThrowIfNull(preview);
		var width = Math.Max(1, preview.PreviewWidth);
		var height = Math.Max(1, preview.PreviewHeight);
		return checked(width * height * 4);
	}

	/// <summary>
	/// Writes one raw BGRA32 preview frame to a reusable bitmap buffer stream.
	/// </summary>
	/// <param name="pixelBuffer">Writable bitmap pixel buffer.</param>
	/// <param name="preview">Preview metadata describing the frame.</param>
	/// <param name="bytes">Raw BGRA32 frame bytes.</param>
	public static void WriteBgra32(Stream pixelBuffer, ImagePreviewDto preview, byte[] bytes)
	{
		ArgumentNullException.ThrowIfNull(pixelBuffer);
		ArgumentNullException.ThrowIfNull(preview);
		ArgumentNullException.ThrowIfNull(bytes);

		var width = Math.Max(1, preview.PreviewWidth);
		var height = Math.Max(1, preview.PreviewHeight);
		var sourceStride = preview.Stride > 0 ? preview.Stride : checked(width * 4);
		var sourceByteCount = checked(sourceStride * height);
		var destinationStride = checked(width * 4);

		if (sourceStride < destinationStride)
			throw new InvalidDataException("BGRA preview stride is smaller than its pixel width.");

		if (bytes.Length < sourceByteCount)
			throw new InvalidDataException("BGRA preview payload is smaller than the declared dimensions.");

		pixelBuffer.Position = 0;
		if (sourceStride == destinationStride)
		{
			pixelBuffer.Write(bytes, 0, sourceByteCount);
			return;
		}

		for (var row = 0; row < height; row++)
			pixelBuffer.Write(bytes, row * sourceStride, destinationStride);
	}

	/// <summary>
	/// Expands a packed Gray8 or RGB24 preview into a reusable tightly packed BGRA32 buffer.
	/// </summary>
	public static void ExpandToBgra32(ImagePreviewDto preview, byte[] source, byte[] destination)
	{
		ArgumentNullException.ThrowIfNull(preview);
		ArgumentNullException.ThrowIfNull(source);
		ArgumentNullException.ThrowIfNull(destination);

		var width = Math.Max(1, preview.PreviewWidth);
		var height = Math.Max(1, preview.PreviewHeight);
		var sourceBytesPerPixel = ImagePreviewEncodingInfo.GetRawBytesPerPixel(preview.Encoding);
		if (preview.Encoding is not (ImagePreviewEncodingDto.Gray8 or ImagePreviewEncodingDto.Rgb24))
			throw new ArgumentException("Only Gray8 and RGB24 previews require BGRA expansion.", nameof(preview));

		var sourceStride = preview.Stride > 0
			? preview.Stride
			: checked(width * sourceBytesPerPixel);
		var minimumSourceStride = checked(width * sourceBytesPerPixel);
		if (sourceStride < minimumSourceStride)
			throw new InvalidDataException("Raw preview stride is smaller than its pixel width.");

		var sourceByteCount = checked(sourceStride * height);
		var destinationByteCount = GetBgra32ByteCount(preview);
		if (source.Length < sourceByteCount)
			throw new InvalidDataException("Raw preview payload is smaller than the declared dimensions.");
		if (destination.Length < destinationByteCount)
			throw new ArgumentException("BGRA destination buffer is smaller than the declared dimensions.", nameof(destination));

		switch (preview.Encoding)
		{
			case ImagePreviewEncodingDto.Gray8:
				ExpandGray8(width, height, sourceStride, source, destination);
				break;
			case ImagePreviewEncodingDto.Rgb24:
				ExpandRgb24(width, height, sourceStride, source, destination);
				break;
		}
	}

	private static void ExpandGray8(int width, int height, int sourceStride, byte[] source, byte[] destination)
	{
		var destinationOffset = 0;
		for (var y = 0; y < height; y++)
		{
			var sourceOffset = y * sourceStride;
			for (var x = 0; x < width; x++)
			{
				var gray = source[sourceOffset + x];
				destination[destinationOffset++] = gray;
				destination[destinationOffset++] = gray;
				destination[destinationOffset++] = gray;
				destination[destinationOffset++] = 255;
			}
		}
	}

	private static void ExpandRgb24(int width, int height, int sourceStride, byte[] source, byte[] destination)
	{
		var destinationOffset = 0;
		for (var y = 0; y < height; y++)
		{
			var sourceOffset = y * sourceStride;
			for (var x = 0; x < width; x++)
			{
				destination[destinationOffset++] = source[sourceOffset + 2];
				destination[destinationOffset++] = source[sourceOffset + 1];
				destination[destinationOffset++] = source[sourceOffset];
				destination[destinationOffset++] = 255;
				sourceOffset += 3;
			}
		}
	}
}
