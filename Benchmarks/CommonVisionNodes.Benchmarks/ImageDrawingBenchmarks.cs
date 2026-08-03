using BenchmarkDotNet.Attributes;
using CommonVisionNodes.Contracts;
using Cvb.Uno.Toolkit.Helpers;

namespace CommonVisionNodes.Benchmarks;

/// <summary>
/// Measures the frontend-owned BGRA upload immediately before bitmap invalidation and composition.
/// </summary>
[MemoryDiagnoser]
public class ImageDrawingBenchmarks
{
    private byte[] _frame = null!;
    private byte[]? _expandedFrame;
    private MemoryStream _reusedPixelBuffer = null!;
    private ImagePreviewDto _preview = null!;

    public IEnumerable<PreviewSize> PreviewSizes =>
    [
        new(640, 480),
        new(1920, 1080)
    ];

    [ParamsSource(nameof(PreviewSizes))]
    public PreviewSize Size { get; set; }

    [Params(ImagePreviewEncodingDto.Gray8, ImagePreviewEncodingDto.Rgb24, ImagePreviewEncodingDto.Bgra32)]
    public ImagePreviewEncodingDto Encoding { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var bytesPerPixel = ImagePreviewEncodingInfo.GetRawBytesPerPixel(Encoding);
        var sourceStride = checked(Size.Width * bytesPerPixel);
        _frame = GC.AllocateUninitializedArray<byte>(checked(sourceStride * Size.Height));
        _expandedFrame = Encoding == ImagePreviewEncodingDto.Bgra32
            ? null
            : GC.AllocateUninitializedArray<byte>(Size.ByteCount);
        _reusedPixelBuffer = new MemoryStream(new byte[Size.ByteCount], writable: true);
        _preview = new ImagePreviewDto
        {
            Encoding = Encoding,
            Width = Size.Width,
            Height = Size.Height,
            PreviewWidth = Size.Width,
            PreviewHeight = Size.Height,
            Stride = sourceStride
        };
    }

    [GlobalCleanup]
    public void Cleanup() => _reusedPixelBuffer.Dispose();

    [Benchmark(Description = "Frontend: expand/upload raw frame to reused pixel buffer")]
    public void UploadRawFrame()
        => PreviewPixelBufferWriter.WriteRawPreview(_reusedPixelBuffer, _preview, _frame, _expandedFrame);
}
