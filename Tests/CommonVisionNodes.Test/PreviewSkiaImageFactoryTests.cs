using CommonVisionNodes.Contracts;
using Cvb.Uno.Toolkit.Helpers;
using SkiaSharp;
using System.Runtime.InteropServices;

namespace CommonVisionNodes.Test;

[TestFixture]
public sealed class PreviewSkiaImageFactoryTests
{
	[Test]
	public void Create_Gray8_PreservesCompactPixelsAndOwnsFrame()
	{
		var preview = CreatePreview(ImagePreviewEncodingDto.Gray8, width: 2, height: 2, stride: 3);
		var source = new byte[] { 10, 20, 99, 30, 40, 88 };
		byte[]? expanded = null;

		using var image = PreviewSkiaImageFactory.Create(preview, source, ref expanded);
		source[0] = 200;

		using var pixels = image.PeekPixels();
		var firstRow = new byte[2];
		var secondRow = new byte[2];
		Marshal.Copy(pixels!.GetPixels(), firstRow, 0, firstRow.Length);
		Marshal.Copy(pixels.GetPixels() + pixels.RowBytes, secondRow, 0, secondRow.Length);
		using (Assert.EnterMultipleScope())
		{
			Assert.That(image.ColorType, Is.EqualTo(SKColorType.Gray8));
			Assert.That(pixels, Is.Not.Null);
			Assert.That(pixels!.RowBytes, Is.EqualTo(3));
			Assert.That(firstRow, Is.EqualTo(new byte[] { 10, 20 }));
			Assert.That(secondRow, Is.EqualTo(new byte[] { 30, 40 }));
			Assert.That(expanded, Is.Null);
		}
	}

	[Test]
	public void Create_Rgb24_ExpandsToOwnedBgraPixels()
	{
		var preview = CreatePreview(ImagePreviewEncodingDto.Rgb24, width: 2, height: 1, stride: 6);
		var source = new byte[] { 1, 2, 3, 10, 20, 30 };
		byte[]? expanded = null;

		using var image = PreviewSkiaImageFactory.Create(preview, source, ref expanded);
		source[0] = 200;

		using var pixels = image.PeekPixels();
		using (Assert.EnterMultipleScope())
		{
			Assert.That(image.ColorType, Is.EqualTo(SKColorType.Bgra8888));
			Assert.That(pixels, Is.Not.Null);
			Assert.That(pixels!.GetPixelSpan().ToArray(), Is.EqualTo(new byte[] { 3, 2, 1, 255, 30, 20, 10, 255 }));
			Assert.That(expanded, Has.Length.EqualTo(8));
		}
	}

	[Test]
	public void Create_RejectsShortRawPayload()
	{
		var preview = CreatePreview(ImagePreviewEncodingDto.Bgra32, width: 2, height: 2, stride: 8);
		byte[]? expanded = null;

		Assert.Throws<InvalidDataException>(() =>
			PreviewSkiaImageFactory.Create(preview, new byte[15], ref expanded));
	}

	private static ImagePreviewDto CreatePreview(
		ImagePreviewEncodingDto encoding,
		int width,
		int height,
		int stride)
		=> new()
		{
			Encoding = encoding,
			Width = width,
			Height = height,
			PreviewWidth = width,
			PreviewHeight = height,
			Stride = stride
		};
}
