using System.IO;
using System.Buffers.Binary;
using System.Net.WebSockets;
using System.Text.Json;
using System.Text.Json.Serialization;
using CommonVisionNodes.Contracts;

namespace CommonVisionNodesUI.Services;

/// <summary>
/// HTTP and WebSocket implementation of <see cref="IBackendClient"/>.
/// </summary>
public sealed class BackendClient : IBackendClient
{
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly Uri _webSocketUriBase;

    /// <summary>
    /// Creates a backend client for a base HTTP URL.
    /// </summary>
    /// <param name="baseUrl">Backend base URL.</param>
    public BackendClient(string baseUrl)
    {
        var normalizedBaseUrl = baseUrl.EndsWith('/') ? baseUrl : $"{baseUrl}/";
        var baseUri = new Uri(normalizedBaseUrl, UriKind.Absolute);
        _httpClient = new HttpClient
        {
            BaseAddress = baseUri
        };
        _webSocketUriBase = BuildWebSocketBaseUri(baseUri);

        _jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        _jsonOptions.Converters.Add(new JsonStringEnumConverter());
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<NodeDefinitionDto>> GetNodeDefinitionsAsync(CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.GetAsync("api/nodes/definitions", cancellationToken);
        response.EnsureSuccessStatusCode();
        return await ReadAsync<List<NodeDefinitionDto>>(response, cancellationToken) ?? [];
    }

    /// <inheritdoc/>
    public async Task<ExecutionAcceptedDto> ExecuteAsync(ExecutionRequestDto request, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsJsonAsync("api/graph/execute", request, _jsonOptions, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await ReadAsync<ExecutionAcceptedDto>(response, cancellationToken) ?? new ExecutionAcceptedDto();
    }

    /// <inheritdoc/>
    public async Task StopAsync(string clientId, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsJsonAsync(
            "api/graph/stop",
            new StopExecutionRequestDto { ClientId = clientId },
            _jsonOptions,
            cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    /// <inheritdoc/>
    public async Task TriggerNodeAsync(TriggerNodeRequestDto request, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsJsonAsync("api/graph/trigger", request, _jsonOptions, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    /// <inheritdoc/>
    public async Task UpdateExecutionSettingsAsync(UpdateExecutionSettingsRequestDto request, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsJsonAsync("api/graph/settings", request, _jsonOptions, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    /// <inheritdoc/>
    public async Task UpdateNodePropertiesAsync(UpdateNodePropertiesRequestDto request, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsJsonAsync("api/graph/node-properties", request, _jsonOptions, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    /// <inheritdoc/>
    public async Task<string> GenerateCodeAsync(GraphDto graph, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsJsonAsync("api/graph/codegen", graph, _jsonOptions, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task ListenAsync(string clientId, Func<ExecutionMessageDto, Task> onMessage, CancellationToken cancellationToken = default)
    {
        using var socket = new ClientWebSocket();
        var websocketUri = new Uri(_webSocketUriBase, $"ws/execution?clientId={Uri.EscapeDataString(clientId)}");
        await socket.ConnectAsync(websocketUri, cancellationToken);

        var buffer = new byte[16 * 1024];
        while (socket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
        {
            var result = await socket.ReceiveAsync(buffer, cancellationToken);
            if (result.MessageType == WebSocketMessageType.Close)
                return;

            var message = result.MessageType == WebSocketMessageType.Binary
                ? await DeserializeBinaryExecutionMessageAsync(socket, buffer, result, cancellationToken)
                : await DeserializeTextExecutionMessageAsync(socket, buffer, result, cancellationToken);
            if (message is not null)
                await onMessage(message);
        }
    }

    private async Task<T?> ReadAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonSerializer.DeserializeAsync<T>(stream, _jsonOptions, cancellationToken);
    }

    private async Task<ExecutionMessageDto?> DeserializeTextExecutionMessageAsync(
        ClientWebSocket socket,
        byte[] buffer,
        WebSocketReceiveResult firstResult,
        CancellationToken cancellationToken)
    {
        using var messageStream = new MemoryStream();
        var result = firstResult;

        while (true)
        {
            messageStream.Write(buffer, 0, result.Count);
            if (result.EndOfMessage)
                break;

            result = await socket.ReceiveAsync(buffer, cancellationToken);
            if (result.MessageType == WebSocketMessageType.Close)
                return null;
        }

        messageStream.Position = 0;
        return await JsonSerializer.DeserializeAsync<ExecutionMessageDto>(messageStream, _jsonOptions, cancellationToken);
    }

    private async Task<ExecutionMessageDto?> DeserializeBinaryExecutionMessageAsync(
        ClientWebSocket socket,
        byte[] buffer,
        WebSocketReceiveResult firstResult,
        CancellationToken cancellationToken)
    {
        var builder = new BinaryExecutionMessageBuilder(_jsonOptions);
        var result = firstResult;

        while (true)
        {
            builder.Append(buffer.AsSpan(0, result.Count));
            if (result.EndOfMessage)
                return builder.Build();

            result = await socket.ReceiveAsync(buffer, cancellationToken);
            if (result.MessageType == WebSocketMessageType.Close)
                return null;
        }
    }

    private static bool TryGetImagePreview(ExecutionMessageDto message, out ImagePreviewDto imagePreview)
    {
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

    private static Uri BuildWebSocketBaseUri(Uri httpBaseUri)
    {
        var builder = new UriBuilder(httpBaseUri)
        {
            Scheme = httpBaseUri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase) ? "wss" : "ws"
        };
        return builder.Uri;
    }

    private sealed class BinaryExecutionMessageBuilder(JsonSerializerOptions jsonOptions)
    {
        private readonly byte[] _header = new byte[sizeof(int)];
        private readonly JsonSerializerOptions _jsonOptions = jsonOptions;
        private byte[]? _metadata;
        private ExecutionMessageDto? _message;
        private byte[]? _imageBytes;
        private MemoryStream? _imageStream;
        private int _headerBytesRead;
        private int _metadataLength = -1;
        private int _metadataBytesRead;
        private int _imageBytesRead;
        private bool _invalid;

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

        public ExecutionMessageDto? Build()
        {
            EnsureMessageParsed();
            if (_invalid || _message is null)
                return null;

            if (TryGetImagePreview(_message, out var imagePreview))
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
                var expectedByteCount = TryGetImagePreview(_message, out var imagePreview)
                    ? GetExpectedRawImageByteCount(imagePreview)
                    : null;

                if (expectedByteCount is > 0)
                    _imageBytes = new byte[expectedByteCount.Value];
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
            if (imagePreview.Encoding != ImagePreviewEncodingDto.Bgra32)
                return null;

            var width = Math.Max(1, imagePreview.PreviewWidth);
            var height = Math.Max(1, imagePreview.PreviewHeight);
            var stride = imagePreview.Stride > 0 ? imagePreview.Stride : checked(width * 4);
            return checked(stride * height);
        }
    }
}
