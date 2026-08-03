using System.Reflection;
using System.Runtime.InteropServices;
using CommonVisionNodes.Contracts;
using CommonVisionNodes.Runtime.Execution;
using Stemmer.Cvb;

namespace CommonVisionNodes.Test;

public sealed class RuntimePreviewFactoryTests
{
    private static readonly MethodInfo CreateImagePreviewMethod = typeof(RuntimePreviewFactory)
        .GetMethod("CreateImagePreview", BindingFlags.NonPublic | BindingFlags.Static)!;

    [Test]
    public void CreateImagePreview_WithRgb8Image_ShouldUseRawBgra32()
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
            Assert.That(preview.Encoding, Is.EqualTo(ImagePreviewEncodingDto.Bgra32));
            Assert.That(preview.MediaType, Is.EqualTo("application/x-bgra32"));
            Assert.That(preview.BinaryData, Is.EqualTo(new byte[] { 30, 20, 10, 255, 60, 50, 40, 255 }));
            Assert.That(preview.PixelFormat, Does.Contain("RGB 8bpp"));
        }
    }

    [Test]
    public void CreateImagePreview_WithMono16Image_ShouldScaleToRawBgra32()
    {
        using var image = new Image(2, 1, 1, PixelDataType.UInt, 16);
        WriteUInt16(image, planeIndex: 0, x: 0, y: 0, value: 0);
        WriteUInt16(image, planeIndex: 0, x: 1, y: 0, value: ushort.MaxValue);

        var preview = CreateImagePreview(image);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(preview.Encoding, Is.EqualTo(ImagePreviewEncodingDto.Bgra32));
            Assert.That(preview.BinaryData, Is.EqualTo(new byte[] { 0, 0, 0, 255, 255, 255, 255, 255 }));
            Assert.That(preview.PixelFormat, Does.Contain("Mono 16bpp"));
        }
    }

    [Test]
    public void CreateImagePreview_WithDownscaledRgbImage_ShouldAverageIntoRawBgra32()
    {
        using var image = new Image(2, 2, 3);
        WriteRgb(image, 0, 0, red: 0, green: 10, blue: 20);
        WriteRgb(image, 1, 0, red: 64, green: 10, blue: 20);
        WriteRgb(image, 0, 1, red: 128, green: 10, blue: 20);
        WriteRgb(image, 1, 1, red: 255, green: 10, blue: 20);

        var preview = CreateImagePreview(image, previewImageMaxDimension: 1);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(preview.Encoding, Is.EqualTo(ImagePreviewEncodingDto.Bgra32));
            Assert.That(preview.PreviewWidth, Is.EqualTo(1));
            Assert.That(preview.PreviewHeight, Is.EqualTo(1));
            Assert.That(preview.BinaryData, Is.EqualTo(new byte[] { 20, 10, 112, 255 }));
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
