using System.Text.Json;
using System.Text.Json.Serialization;
using BenchmarkDotNet.Attributes;
using CommonVisionNodes.Contracts;
using Cvb.Uno.Toolkit.Helpers;

namespace CommonVisionNodes.Benchmarks;

/// <summary>
/// Measures the production binary image protocol at the backend/frontend boundary.
/// </summary>
[MemoryDiagnoser]
public class ImageTransferBenchmarks
{
    private const int LegacyFrontendReceiveBufferSize = 16 * 1024;
    private const int FrontendReceiveBufferSize = 64 * 1024;

    private BinaryImageBufferCache _imageBufferCache = null!;
    private JsonSerializerOptions _jsonOptions = null!;
    private ExecutionMessageDto _message = null!;
    private byte[] _metadata = null!;
    private byte[] _metadataHeader = null!;
    private byte[] _payload = null!;
    private MemoryStream _pixelBuffer = null!;

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
        _jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        _jsonOptions.Converters.Add(new JsonStringEnumConverter());

        _payload = CreateFrame(Size.ByteCount);
        _pixelBuffer = new MemoryStream(new byte[Size.ByteCount], writable: true);
        _message = new ExecutionMessageDto
        {
            ExecutionId = "benchmark-execution",
            MessageType = ExecutionMessageTypeDto.ImagePreview,
            ImagePreview = new ImagePreviewDto
            {
                NodeId = "benchmark-node",
                MediaType = "application/x-bgra32",
                Encoding = ImagePreviewEncodingDto.Bgra32,
                BinaryData = _payload,
                Width = Size.Width,
                Height = Size.Height,
                PreviewWidth = Size.Width,
                PreviewHeight = Size.Height,
                Stride = Size.Stride,
                PixelFormat = "BGRA 8bpp",
                TimestampUtc = DateTimeOffset.UnixEpoch
            },
            TimestampUtc = DateTimeOffset.UnixEpoch
        };

        _metadata = BinaryExecutionMessageCodec.SerializeMetadata(_message, _jsonOptions);
        _metadataHeader = BinaryExecutionMessageCodec.CreateMetadataLengthHeader(_metadata.Length);
        _imageBufferCache = new BinaryImageBufferCache();

        // Populate both alternating buffers so steady-state measurements do not include cache warmup.
        _imageBufferCache.GetNextBuffer(_message.ImagePreview, Size.ByteCount);
        _imageBufferCache.GetNextBuffer(_message.ImagePreview, Size.ByteCount);
    }

    [GlobalCleanup]
    public void Cleanup() => _pixelBuffer.Dispose();

    [Benchmark(Description = "Backend: serialize image metadata")]
    public byte[] BackendSerializeMetadata()
        => BinaryExecutionMessageCodec.SerializeMetadata(_message, _jsonOptions);

    [Benchmark(Baseline = true, Description = "Frontend baseline: allocate, 16 KiB chunks")]
    public ExecutionMessageDto FrontendReceiveAllocatingBuffer()
        => ReceiveMessage(
            _metadataHeader,
            _metadata,
            _payload,
            receiveBufferSize: LegacyFrontendReceiveBufferSize,
            imageBufferCache: null);

    [Benchmark(Description = "Frontend optimized: reuse, 64 KiB chunks")]
    public ExecutionMessageDto FrontendReceiveReusedBuffer()
        => ReceiveMessage(
            _metadataHeader,
            _metadata,
            _payload,
            FrontendReceiveBufferSize,
            _imageBufferCache);

    [Benchmark(Description = "Backend-to-frontend protocol round trip")]
    public ExecutionMessageDto BinaryProtocolRoundTrip()
    {
        var imageBytes = BinaryExecutionMessageCodec.GetImageBytes(_message.ImagePreview!)!;
        var metadata = BinaryExecutionMessageCodec.SerializeMetadata(_message, _jsonOptions);
        var header = BinaryExecutionMessageCodec.CreateMetadataLengthHeader(metadata.Length);
        return ReceiveMessage(header, metadata, imageBytes, FrontendReceiveBufferSize, _imageBufferCache);
    }

    [Benchmark(Description = "Frontend optimized: receive and upload BGRA")]
    public ExecutionMessageDto FrontendReceiveAndUpload()
    {
        var message = ReceiveMessage(
            _metadataHeader,
            _metadata,
            _payload,
            FrontendReceiveBufferSize,
            _imageBufferCache);
        var imagePreview = message.ImagePreview!;
        PreviewPixelBufferWriter.WriteBgra32(_pixelBuffer, imagePreview, imagePreview.BinaryData!);
        return message;
    }

    private ExecutionMessageDto ReceiveMessage(
        byte[] header,
        byte[] metadata,
        byte[] payload,
        int receiveBufferSize,
        BinaryImageBufferCache? imageBufferCache)
    {
        var builder = new BinaryExecutionMessageBuilder(_jsonOptions, imageBufferCache);
        builder.Append(header);
        builder.Append(metadata);

        for (var offset = 0; offset < payload.Length; offset += receiveBufferSize)
        {
            var count = Math.Min(receiveBufferSize, payload.Length - offset);
            builder.Append(payload.AsSpan(offset, count));
        }

        return builder.Build() ?? throw new InvalidOperationException("The benchmark payload could not be reconstructed.");
    }

    private static byte[] CreateFrame(int byteCount)
    {
        var bytes = GC.AllocateUninitializedArray<byte>(byteCount);
        for (var index = 0; index < bytes.Length; index++)
            bytes[index] = unchecked((byte)(index * 31));

        return bytes;
    }
}
