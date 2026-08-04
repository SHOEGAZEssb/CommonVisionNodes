using System.Buffers.Binary;
using System.Text.Json;

namespace CommonVisionNodes.Contracts;

/// <summary>
/// Encodes and decodes execution messages that carry image data in a binary WebSocket message.
/// </summary>
/// <remarks>
/// The wire format starts with a four-byte little-endian metadata length, followed by JSON
/// metadata and the unencoded image bytes. The image bytes may be split across any number of
/// receive buffers.
/// </remarks>
public static class BinaryExecutionMessageCodec
{
	/// <summary>
	/// Serializes the JSON portion of a binary execution message without its image bytes.
	/// </summary>
	/// <param name="message">Message to serialize.</param>
	/// <param name="jsonOptions">JSON options shared by the server and client.</param>
	/// <returns>UTF-8 encoded metadata.</returns>
	public static byte[] SerializeMetadata(ExecutionMessageDto message, JsonSerializerOptions jsonOptions)
	{
		ArgumentNullException.ThrowIfNull(message);
		ArgumentNullException.ThrowIfNull(jsonOptions);

		return JsonSerializer.SerializeToUtf8Bytes(CloneWithoutImageData(message), jsonOptions);
	}

	/// <summary>
	/// Creates the fixed-size header that precedes binary execution message metadata.
	/// </summary>
	/// <param name="metadataLength">Metadata size in bytes.</param>
	/// <returns>A four-byte little-endian header.</returns>
	public static byte[] CreateMetadataLengthHeader(int metadataLength)
	{
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(metadataLength);

		var header = new byte[sizeof(int)];
		BinaryPrimitives.WriteInt32LittleEndian(header, metadataLength);
		return header;
	}

	/// <summary>
	/// Gets the raw image bytes carried by a preview, decoding the legacy base64 payload when necessary.
	/// </summary>
	/// <param name="imagePreview">Image preview to inspect.</param>
	/// <returns>The image bytes, or <c>null</c> when the preview has no image data.</returns>
	public static byte[]? GetImageBytes(ImagePreviewDto imagePreview)
	{
		ArgumentNullException.ThrowIfNull(imagePreview);

		if (imagePreview.BinaryData is { Length: > 0 } binaryData)
			return binaryData;

		return string.IsNullOrWhiteSpace(imagePreview.Base64Data)
			? null
			: Convert.FromBase64String(imagePreview.Base64Data);
	}

	/// <summary>
	/// Finds the image preview carried directly or inside an overlay preview message.
	/// </summary>
	/// <param name="message">Execution message to inspect.</param>
	/// <param name="imagePreview">The contained image preview when found.</param>
	/// <returns><c>true</c> when the message contains an image preview.</returns>
	public static bool TryGetImagePreview(ExecutionMessageDto message, out ImagePreviewDto imagePreview)
	{
		ArgumentNullException.ThrowIfNull(message);
		imagePreview = null!;

		switch (message.MessageType)
		{
			case ExecutionMessageTypeDto.ImagePreview when message.ImagePreview is not null:
				imagePreview = message.ImagePreview;
				return true;
			case ExecutionMessageTypeDto.BlobPreview when message.BlobPreview?.Image is not null:
				imagePreview = message.BlobPreview.Image;
				return true;
			case ExecutionMessageTypeDto.ClassificationPreview when message.ClassificationPreview?.Image is not null:
				imagePreview = message.ClassificationPreview.Image;
				return true;
			case ExecutionMessageTypeDto.CodeReaderPreview when message.CodeReaderPreview?.Image is not null:
				imagePreview = message.CodeReaderPreview.Image;
				return true;
			default:
				return false;
		}
	}

	private static ExecutionMessageDto CloneWithoutImageData(ExecutionMessageDto message)
		=> new()
		{
			ExecutionId = message.ExecutionId,
			MessageType = message.MessageType,
			ExecutionState = message.ExecutionState,
			NodeUpdate = message.NodeUpdate,
			ImagePreview = message.ImagePreview is null ? null : CloneImageMetadata(message.ImagePreview),
			HistogramPreview = message.HistogramPreview,
			BlobPreview = message.BlobPreview is null ? null : new BlobPreviewDto
			{
				NodeId = message.BlobPreview.NodeId,
				Image = message.BlobPreview.Image is null ? null : CloneImageMetadata(message.BlobPreview.Image),
				Blobs = message.BlobPreview.Blobs,
				TimestampUtc = message.BlobPreview.TimestampUtc
			},
			ClassificationPreview = message.ClassificationPreview is null ? null : new ClassificationPreviewDto
			{
				NodeId = message.ClassificationPreview.NodeId,
				Image = message.ClassificationPreview.Image is null ? null : CloneImageMetadata(message.ClassificationPreview.Image),
				Results = message.ClassificationPreview.Results,
				TimestampUtc = message.ClassificationPreview.TimestampUtc
			},
			CodeReaderPreview = message.CodeReaderPreview is null ? null : new CodeReaderPreviewDto
			{
				NodeId = message.CodeReaderPreview.NodeId,
				Image = message.CodeReaderPreview.Image is null ? null : CloneImageMetadata(message.CodeReaderPreview.Image),
				Results = message.CodeReaderPreview.Results,
				TimeLimitReached = message.CodeReaderPreview.TimeLimitReached,
				TimestampUtc = message.CodeReaderPreview.TimestampUtc
			},
			TextPreview = message.TextPreview,
			Error = message.Error,
			TimestampUtc = message.TimestampUtc
		};

	private static ImagePreviewDto CloneImageMetadata(ImagePreviewDto imagePreview)
		=> new()
		{
			PreviewSequence = imagePreview.PreviewSequence,
			NodeId = imagePreview.NodeId,
			MediaType = imagePreview.MediaType,
			Encoding = imagePreview.Encoding,
			Width = imagePreview.Width,
			Height = imagePreview.Height,
			PreviewWidth = imagePreview.PreviewWidth,
			PreviewHeight = imagePreview.PreviewHeight,
			Stride = imagePreview.Stride,
			PixelFormat = imagePreview.PixelFormat,
			TimestampUtc = imagePreview.TimestampUtc
		};
}

/// <summary>
/// Incrementally reconstructs one binary execution message from WebSocket receive buffers.
/// </summary>
public sealed class BinaryExecutionMessageBuilder
{
	private readonly byte[] _header = new byte[sizeof(int)];
	private readonly JsonSerializerOptions _jsonOptions;
	private readonly BinaryImageBufferCache? _imageBufferCache;
	private byte[]? _metadata;
	private ExecutionMessageDto? _message;
	private byte[]? _imageBytes;
	private MemoryStream? _imageStream;
	private int _headerBytesRead;
	private int _metadataLength = -1;
	private int _metadataBytesRead;
	private int _imageBytesRead;
	private bool _invalid;

	/// <summary>
	/// Creates a binary execution message builder.
	/// </summary>
	/// <param name="jsonOptions">JSON options shared by the server and client.</param>
	/// <param name="imageBufferCache">Optional cache used to reuse raw image destination buffers.</param>
	public BinaryExecutionMessageBuilder(
		JsonSerializerOptions jsonOptions,
		BinaryImageBufferCache? imageBufferCache = null)
	{
		ArgumentNullException.ThrowIfNull(jsonOptions);
		_jsonOptions = jsonOptions;
		_imageBufferCache = imageBufferCache;
	}

	/// <summary>
	/// Appends the next received portion of the binary WebSocket message.
	/// </summary>
	/// <param name="data">Received bytes in wire order.</param>
	public void Append(ReadOnlySpan<byte> data)
	{
		while (!data.IsEmpty && !_invalid)
		{
			if (_headerBytesRead < sizeof(int))
			{
				var bytesToCopy = Math.Min(sizeof(int) - _headerBytesRead, data.Length);
				data[..bytesToCopy].CopyTo(_header.AsSpan(_headerBytesRead));
				_headerBytesRead += bytesToCopy;
				data = data[bytesToCopy..];

				if (_headerBytesRead == sizeof(int))
				{
					_metadataLength = BinaryPrimitives.ReadInt32LittleEndian(_header);
					if (_metadataLength <= 0)
					{
						_invalid = true;
						return;
					}

					_metadata = new byte[_metadataLength];
				}

				continue;
			}

			if (_metadata is not null && _metadataBytesRead < _metadata.Length)
			{
				var bytesToCopy = Math.Min(_metadata.Length - _metadataBytesRead, data.Length);
				data[..bytesToCopy].CopyTo(_metadata.AsSpan(_metadataBytesRead));
				_metadataBytesRead += bytesToCopy;
				data = data[bytesToCopy..];

				if (_metadataBytesRead == _metadata.Length)
					EnsureMessageParsed();

				continue;
			}

			AppendImageBytes(data);
			break;
		}
	}

	/// <summary>
	/// Completes the message after its final receive buffer has been appended.
	/// </summary>
	/// <returns>The reconstructed message, or <c>null</c> when the payload is invalid or incomplete.</returns>
	public ExecutionMessageDto? Build()
	{
		EnsureMessageParsed();
		if (_invalid || _message is null)
			return null;

		if (BinaryExecutionMessageCodec.TryGetImagePreview(_message, out var imagePreview))
		{
			if (_imageBytes is not null)
			{
				if (_imageBytesRead != _imageBytes.Length)
					return null;

				imagePreview.BinaryData = _imageBytes;
			}
			else
			{
				imagePreview.BinaryData = _imageStream?.ToArray() ?? [];
			}
		}

		_imageStream?.Dispose();
		return _message;
	}

	private void EnsureMessageParsed()
	{
		if (_message is not null || _metadata is null || _metadataBytesRead != _metadata.Length || _invalid)
			return;

		_message = JsonSerializer.Deserialize<ExecutionMessageDto>(_metadata, _jsonOptions);
		if (_message is null)
			_invalid = true;
	}

	private void AppendImageBytes(ReadOnlySpan<byte> data)
	{
		if (data.IsEmpty)
			return;

		EnsureMessageParsed();
		if (_message is null)
		{
			_invalid = true;
			return;
		}

		if (_imageBytes is null && _imageStream is null)
		{
			var expectedByteCount = BinaryExecutionMessageCodec.TryGetImagePreview(_message, out var imagePreview)
				? GetExpectedRawImageByteCount(imagePreview)
				: null;

			if (expectedByteCount is > 0)
			{
				_imageBytes = _imageBufferCache?.GetNextBuffer(imagePreview, expectedByteCount.Value)
					?? GC.AllocateUninitializedArray<byte>(expectedByteCount.Value);
			}
			else
				_imageStream = new MemoryStream();
		}

		if (_imageBytes is not null)
		{
			var remainingByteCount = _imageBytes.Length - _imageBytesRead;
			if (data.Length > remainingByteCount)
			{
				_invalid = true;
				return;
			}

			data.CopyTo(_imageBytes.AsSpan(_imageBytesRead));
			_imageBytesRead += data.Length;
			return;
		}

		_imageStream!.Write(data);
	}

	private static int? GetExpectedRawImageByteCount(ImagePreviewDto imagePreview)
	{
		var bytesPerPixel = ImagePreviewEncodingInfo.GetRawBytesPerPixel(imagePreview.Encoding);
		if (bytesPerPixel == 0)
			return null;

		var width = Math.Max(1, imagePreview.PreviewWidth);
		var height = Math.Max(1, imagePreview.PreviewHeight);
		var minimumStride = checked(width * bytesPerPixel);
		var stride = imagePreview.Stride > 0 ? Math.Max(imagePreview.Stride, minimumStride) : minimumStride;
		return checked(stride * height);
	}
}
