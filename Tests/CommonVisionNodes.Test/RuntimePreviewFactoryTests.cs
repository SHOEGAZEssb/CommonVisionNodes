using System.Reflection;
using System.Runtime.InteropServices;
using CommonVisionNodes.Contracts;
using CommonVisionNodes.Runtime;
using CommonVisionNodes.Runtime.Definitions;
using CommonVisionNodes.Runtime.Execution;
using Stemmer.Cvb;

namespace CommonVisionNodes.Test;

public sealed class RuntimePreviewFactoryTests
{
	private static readonly MethodInfo CreateImagePreviewMethod = typeof(RuntimePreviewFactory)
		.GetMethod("CreateImagePreview", BindingFlags.NonPublic | BindingFlags.Static)!;

	[Test]
	public void CreateImagePreview_WithRgb8Image_ShouldUsePackedRgb24()
	{
		using var image = new Image(2, 1, 3);
		WriteByte(image, planeIndex: 0, x: 0, y: 0, value: 10);
		WriteByte(image, planeIndex: 1, x: 0, y: 0, value: 20);
		WriteByte(image, planeIndex: 2, x: 0, y: 0, value: 30);
		WriteByte(image, planeIndex: 0, x: 1, y: 0, value: 40);
		WriteByte(image, planeIndex: 1, x: 1, y: 0, value: 50);
		WriteByte(image, planeIndex: 2, x: 1, y: 0, value: 60);

		var preview = CreateImagePreview(image);

		using (Assert.EnterMultipleScope())
		{
			Assert.That(preview.Encoding, Is.EqualTo(ImagePreviewEncodingDto.Rgb24));
			Assert.That(preview.MediaType, Is.EqualTo("application/x-rgb24"));
			Assert.That(preview.Stride, Is.EqualTo(6));
			Assert.That(preview.BinaryData, Is.EqualTo(new byte[] { 10, 20, 30, 40, 50, 60 }));
			Assert.That(preview.PixelFormat, Does.Contain("RGB 8bpp"));
		}
	}

	[Test]
	public void CreateImagePreview_WithMono16Image_ShouldScaleToPackedGray8()
	{
		using var image = new Image(2, 1, 1, PixelDataType.UInt, 16);
		WriteUInt16(image, planeIndex: 0, x: 0, y: 0, value: 0);
		WriteUInt16(image, planeIndex: 0, x: 1, y: 0, value: ushort.MaxValue);

		var preview = CreateImagePreview(image);

		using (Assert.EnterMultipleScope())
		{
			Assert.That(preview.Encoding, Is.EqualTo(ImagePreviewEncodingDto.Gray8));
			Assert.That(preview.MediaType, Is.EqualTo("application/x-gray8"));
			Assert.That(preview.Stride, Is.EqualTo(2));
			Assert.That(preview.BinaryData, Is.EqualTo(new byte[] { 0, 255 }));
			Assert.That(preview.PixelFormat, Does.Contain("Mono 16bpp"));
		}
	}

	[Test]
	public void CreateImagePreview_WithDownscaledRgbImage_ShouldAverageIntoPackedRgb24()
	{
		using var image = new Image(2, 2, 3);
		WriteRgb(image, 0, 0, red: 0, green: 10, blue: 20);
		WriteRgb(image, 1, 0, red: 64, green: 10, blue: 20);
		WriteRgb(image, 0, 1, red: 128, green: 10, blue: 20);
		WriteRgb(image, 1, 1, red: 255, green: 10, blue: 20);

		var preview = CreateImagePreview(image, previewImageMaxDimension: 1);

		using (Assert.EnterMultipleScope())
		{
			Assert.That(preview.Encoding, Is.EqualTo(ImagePreviewEncodingDto.Rgb24));
			Assert.That(preview.PreviewWidth, Is.EqualTo(1));
			Assert.That(preview.PreviewHeight, Is.EqualTo(1));
			Assert.That(preview.BinaryData, Is.EqualTo(new byte[] { 112, 10, 20 }));
		}
	}

	[Test]
	public void CreateImagePreview_WithRgba8Image_ShouldRetainRawBgra32()
	{
		using var image = new Image(1, 1, 4);
		WriteByte(image, planeIndex: 0, x: 0, y: 0, value: 10);
		WriteByte(image, planeIndex: 1, x: 0, y: 0, value: 20);
		WriteByte(image, planeIndex: 2, x: 0, y: 0, value: 30);
		WriteByte(image, planeIndex: 3, x: 0, y: 0, value: 40);

		var preview = CreateImagePreview(image);

		using (Assert.EnterMultipleScope())
		{
			Assert.That(preview.Encoding, Is.EqualTo(ImagePreviewEncodingDto.Bgra32));
			Assert.That(preview.MediaType, Is.EqualTo("application/x-bgra32"));
			Assert.That(preview.BinaryData, Is.EqualTo(new byte[] { 30, 20, 10, 40 }));
		}
	}

	[Test]
	public void CreateImagePreview_WithBufferCache_ShouldAlternateAndReuseBuffers()
	{
		using var image = new Image(2, 1, 1);
		var cache = new BinaryImageBufferCache();

		var first = CreateImagePreview(image, imageBufferCache: cache);
		var second = CreateImagePreview(image, imageBufferCache: cache);
		var third = CreateImagePreview(image, imageBufferCache: cache);

		using (Assert.EnterMultipleScope())
		{
			Assert.That(second.BinaryData, Is.Not.SameAs(first.BinaryData));
			Assert.That(third.BinaryData, Is.SameAs(first.BinaryData));
		}
	}

	[Test]
	public void BlobPreview_AppliesCurrentMaxBlobCountToPreviousFrameResults()
	{
		using var image = new Image(9, 1, 1);
		for (var x = 0; x < image.Width; x++)
			WriteByte(image, planeIndex: 0, x, y: 0, value: x % 2 == 0 ? byte.MaxValue : byte.MinValue);

		var node = new BlobNode { MaxBlobCount = 0 };
		node.ImageInput.Value = image;
		node.Execute();
		Assert.That(node.Blobs, Has.Count.EqualTo(5));

		// Simulate a live property edit after this frame executed but before its preview is created.
		node.MaxBlobCount = 2;
		var message = RuntimePreviewFactory.CreatePreviewMessage("blob-node", node, previewImageMaxDimension: 0);

		Assert.That(message?.BlobPreview?.Blobs, Has.Count.EqualTo(2));
	}

	[Test]
	public void BlobNode_DefaultMaxBlobCount_IsTenInRuntimeAndCatalog()
	{
		var node = new BlobNode();
		var definition = new RuntimeNodeCatalog().GetDefinition(nameof(BlobNode));
		var property = definition?.Properties.Single(item => item.Name == nameof(BlobNode.MaxBlobCount));

		using (Assert.EnterMultipleScope())
		{
			Assert.That(node.MaxBlobCount, Is.EqualTo(10));
			Assert.That(property?.DefaultValue, Is.EqualTo("10"));
		}
	}

	private static ImagePreviewDto CreateImagePreview(
		Image image,
		int previewImageMaxDimension = 0,
		BinaryImageBufferCache? imageBufferCache = null)
		=> (ImagePreviewDto)CreateImagePreviewMethod.Invoke(
			null,
			["preview-node", image, previewImageMaxDimension, imageBufferCache])!;

	private static void WriteRgb(Image image, int x, int y, byte red, byte green, byte blue)
	{
		WriteByte(image, 0, x, y, red);
		WriteByte(image, 1, x, y, green);
		WriteByte(image, 2, x, y, blue);
	}

	private static void WriteByte(Image image, int planeIndex, int x, int y, byte value)
	{
		var access = image.Planes[planeIndex].GetLinearAccess();
		var pixel = access.BasePtr + checked((nint)(y * access.YInc + x * access.XInc));
		Marshal.WriteByte(pixel, value);
	}

	private static void WriteUInt16(Image image, int planeIndex, int x, int y, ushort value)
	{
		var access = image.Planes[planeIndex].GetLinearAccess();
		var pixel = access.BasePtr + checked((nint)(y * access.YInc + x * access.XInc));
		Marshal.WriteInt16(pixel, unchecked((short)value));
	}
}
