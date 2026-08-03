using CommonVisionNodes.Contracts;

namespace Cvb.Uno.Toolkit.Helpers;

/// <summary>
/// Copies raw BGRA preview data into a frontend bitmap pixel buffer.
/// </summary>
public static class PreviewPixelBufferWriter
{
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
        var stride = preview.Stride > 0 ? preview.Stride : checked(width * 4);
        var expectedByteCount = checked(stride * height);

        if (bytes.Length < expectedByteCount)
            throw new InvalidDataException("BGRA preview payload is smaller than the declared dimensions.");

        pixelBuffer.Position = 0;
        pixelBuffer.Write(bytes, 0, expectedByteCount);
    }
}
