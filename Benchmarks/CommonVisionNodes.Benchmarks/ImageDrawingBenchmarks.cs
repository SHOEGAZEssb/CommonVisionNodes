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
    private MemoryStream _reusedPixelBuffer = null!;
    private ImagePreviewDto _preview = null!;

    public IEnumerable<PreviewSize> PreviewSizes =>
    [
        new(640, 480),
        new(1920, 1080)
    ];

    [ParamsSource(nameof(PreviewSizes))]
    public PreviewSize Size { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _frame = GC.AllocateUninitializedArray<byte>(Size.ByteCount);
        _reusedPixelBuffer = new MemoryStream(new byte[Size.ByteCount], writable: true);
        _preview = new ImagePreviewDto
        {
            Encoding = ImagePreviewEncodingDto.Bgra32,
            Width = Size.Width,
            Height = Size.Height,
            PreviewWidth = Size.Width,
            PreviewHeight = Size.Height,
            Stride = Size.Stride
        };
    }

    [GlobalCleanup]
    public void Cleanup() => _reusedPixelBuffer.Dispose();

    [Benchmark(Description = "Frontend: upload BGRA frame to reused pixel buffer")]
    public void UploadBgraFrame()
        => PreviewPixelBufferWriter.WriteBgra32(_reusedPixelBuffer, _preview, _frame);
}
