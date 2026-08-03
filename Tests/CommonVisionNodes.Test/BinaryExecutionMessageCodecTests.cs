using System.Text.Json;
using System.Text.Json.Serialization;
using CommonVisionNodes.Contracts;

namespace CommonVisionNodes.Test;

public sealed class BinaryExecutionMessageCodecTests
{
    private JsonSerializerOptions _jsonOptions = null!;

    [SetUp]
    public void SetUp()
    {
        _jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        _jsonOptions.Converters.Add(new JsonStringEnumConverter());
    }

    [TestCase(1)]
    [TestCase(3)]
    [TestCase(16 * 1024)]
    public void Build_WithFragmentedBgraPayload_ShouldRestoreImageBytes(int receiveBufferSize)
    {
        var payload = Enumerable.Range(0, 8 * 4).Select(index => (byte)index).ToArray();
        var message = CreateImageMessage(ImagePreviewEncodingDto.Bgra32, payload, width: 8, height: 1);

        var result = RoundTrip(message, payload, receiveBufferSize);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.MessageType, Is.EqualTo(ExecutionMessageTypeDto.ImagePreview));
            Assert.That(result.ImagePreview?.BinaryData, Is.EqualTo(payload));
            Assert.That(result.ImagePreview?.Base64Data, Is.Empty);
        }
    }

    [Test]
    public void Build_WithPngOverlayPreview_ShouldRestoreUnknownLengthPayload()
    {
        byte[] payload = [137, 80, 78, 71, 1, 2, 3, 4, 5];
        var image = CreateImagePreview(ImagePreviewEncodingDto.Png, payload, width: 2, height: 2);
        var message = new ExecutionMessageDto
        {
            ExecutionId = "execution",
            MessageType = ExecutionMessageTypeDto.BlobPreview,
            BlobPreview = new BlobPreviewDto
            {
                NodeId = "blob-node",
                Image = image,
                Blobs = [new BlobInfoDto { Label = 1, Area = 4 }]
            }
        };

        var result = RoundTrip(message, payload, receiveBufferSize: 2);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.BlobPreview?.Image?.BinaryData, Is.EqualTo(payload));
            Assert.That(result.BlobPreview?.Blobs, Has.Count.EqualTo(1));
        }
    }

    [Test]
    public void SerializeMetadata_ShouldExcludeBinaryAndLegacyImageData()
    {
        byte[] payload = [1, 2, 3, 4];
        var message = CreateImageMessage(ImagePreviewEncodingDto.Bgra32, payload, width: 1, height: 1);
        message.ImagePreview!.PreviewSequence = 42;
        message.ImagePreview!.Base64Data = Convert.ToBase64String(payload);

        var metadata = BinaryExecutionMessageCodec.SerializeMetadata(message, _jsonOptions);
        var deserialized = JsonSerializer.Deserialize<ExecutionMessageDto>(metadata, _jsonOptions);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(deserialized?.ImagePreview?.BinaryData, Is.Null);
            Assert.That(deserialized?.ImagePreview?.Base64Data, Is.Empty);
            Assert.That(deserialized?.ImagePreview?.PreviewSequence, Is.EqualTo(42));
            Assert.That(message.ImagePreview.BinaryData, Is.SameAs(payload));
            Assert.That(message.ImagePreview.Base64Data, Is.Not.Empty);
        }
    }

    [Test]
    public void Build_WithIncompleteBgraPayload_ShouldReturnNull()
    {
        byte[] payload = [1, 2, 3, 4];
        var message = CreateImageMessage(ImagePreviewEncodingDto.Bgra32, payload, width: 2, height: 1);
        var metadata = BinaryExecutionMessageCodec.SerializeMetadata(message, _jsonOptions);
        var builder = new BinaryExecutionMessageBuilder(_jsonOptions);

        builder.Append(BinaryExecutionMessageCodec.CreateMetadataLengthHeader(metadata.Length));
        builder.Append(metadata);
        builder.Append(payload);

        Assert.That(builder.Build(), Is.Null);
    }

    [Test]
    public void Build_WithImageBufferCache_ShouldAlternateAndReuseExactSizeBuffers()
    {
        var payload = Enumerable.Range(0, 8 * 4).Select(index => (byte)index).ToArray();
        var message = CreateImageMessage(ImagePreviewEncodingDto.Bgra32, payload, width: 8, height: 1);
        var metadata = BinaryExecutionMessageCodec.SerializeMetadata(message, _jsonOptions);
        var header = BinaryExecutionMessageCodec.CreateMetadataLengthHeader(metadata.Length);
        var cache = new BinaryImageBufferCache();

        var first = ReceiveWithCache(header, metadata, payload, cache);
        var second = ReceiveWithCache(header, metadata, payload, cache);
        var third = ReceiveWithCache(header, metadata, payload, cache);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(first.ImagePreview?.BinaryData, Has.Length.EqualTo(payload.Length));
            Assert.That(second.ImagePreview?.BinaryData, Is.Not.SameAs(first.ImagePreview?.BinaryData));
            Assert.That(third.ImagePreview?.BinaryData, Is.SameAs(first.ImagePreview?.BinaryData));
            Assert.That(third.ImagePreview?.BinaryData, Is.EqualTo(payload));
        }
    }

    private ExecutionMessageDto RoundTrip(ExecutionMessageDto message, byte[] payload, int receiveBufferSize)
    {
        var metadata = BinaryExecutionMessageCodec.SerializeMetadata(message, _jsonOptions);
        var header = BinaryExecutionMessageCodec.CreateMetadataLengthHeader(metadata.Length);
        var wirePayload = header.Concat(metadata).Concat(payload).ToArray();
        var builder = new BinaryExecutionMessageBuilder(_jsonOptions);

        for (var offset = 0; offset < wirePayload.Length; offset += receiveBufferSize)
        {
            var count = Math.Min(receiveBufferSize, wirePayload.Length - offset);
            builder.Append(wirePayload.AsSpan(offset, count));
        }

        return builder.Build() ?? throw new AssertionException("The binary message was not reconstructed.");
    }

    private ExecutionMessageDto ReceiveWithCache(
        byte[] header,
        byte[] metadata,
        byte[] payload,
        BinaryImageBufferCache cache)
    {
        var builder = new BinaryExecutionMessageBuilder(_jsonOptions, cache);
        builder.Append(header);
        builder.Append(metadata);
        builder.Append(payload);
        return builder.Build() ?? throw new AssertionException("The cached binary message was not reconstructed.");
    }

    private static ExecutionMessageDto CreateImageMessage(
        ImagePreviewEncodingDto encoding,
        byte[] payload,
        int width,
        int height)
        => new()
        {
            ExecutionId = "execution",
            MessageType = ExecutionMessageTypeDto.ImagePreview,
            ImagePreview = CreateImagePreview(encoding, payload, width, height)
        };

    private static ImagePreviewDto CreateImagePreview(
        ImagePreviewEncodingDto encoding,
        byte[] payload,
        int width,
        int height)
        => new()
        {
            NodeId = "image-node",
            MediaType = encoding == ImagePreviewEncodingDto.Bgra32 ? "application/x-bgra32" : "image/png",
            Encoding = encoding,
            BinaryData = payload,
            Width = width,
            Height = height,
            PreviewWidth = width,
            PreviewHeight = height,
            Stride = encoding == ImagePreviewEncodingDto.Bgra32 ? width * 4 : 0,
            PixelFormat = "test"
        };
}
