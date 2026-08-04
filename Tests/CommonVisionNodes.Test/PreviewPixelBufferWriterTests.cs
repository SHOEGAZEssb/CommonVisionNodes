using CommonVisionNodes.Contracts;
using Cvb.Uno.Toolkit.Helpers;

namespace CommonVisionNodes.Test;

public sealed class PreviewPixelBufferWriterTests
{
	[Test]
	public void ExpandToBgra32_WithPaddedGray8Rows_ShouldReplicateIntensity()
	{
		var preview = CreatePreview(ImagePreviewEncodingDto.Gray8, width: 2, height: 2, stride: 3);
		byte[] source = [0, 127, 99, 255, 64, 99];
		var destination = new byte[PreviewPixelBufferWriter.GetBgra32ByteCount(preview)];

		PreviewPixelBufferWriter.ExpandToBgra32(preview, source, destination);

		Assert.That(destination, Is.EqualTo(new byte[]
		{
			0, 0, 0, 255,
			127, 127, 127, 255,
			255, 255, 255, 255,
			64, 64, 64, 255
		}));
	}

	[Test]
	public void ExpandToBgra32_WithRgb24_ShouldSwapRedAndBlue()
	{
		var preview = CreatePreview(ImagePreviewEncodingDto.Rgb24, width: 2, height: 1, stride: 6);
		byte[] source = [10, 20, 30, 40, 50, 60];
		var destination = new byte[PreviewPixelBufferWriter.GetBgra32ByteCount(preview)];

		PreviewPixelBufferWriter.ExpandToBgra32(preview, source, destination);

		Assert.That(destination, Is.EqualTo(new byte[]
		{
			30, 20, 10, 255,
			60, 50, 40, 255
		}));
	}

	[Test]
	public void WriteBgra32_WithPaddedRows_ShouldExcludePadding()
	{
		var preview = CreatePreview(ImagePreviewEncodingDto.Bgra32, width: 1, height: 2, stride: 8);
		byte[] source =
		[
			1, 2, 3, 4, 99, 99, 99, 99,
			5, 6, 7, 8, 99, 99, 99, 99
		];
		using var destination = new MemoryStream(new byte[8], writable: true);

		PreviewPixelBufferWriter.WriteBgra32(destination, preview, source);

		Assert.That(destination.ToArray(), Is.EqualTo(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }));
	}

	private static ImagePreviewDto CreatePreview(
		ImagePreviewEncodingDto encoding,
		int width,
		int height,
		int stride)
		=> new()
		{
			Encoding = encoding,
			PreviewWidth = width,
			PreviewHeight = height,
			Stride = stride
		};
}
