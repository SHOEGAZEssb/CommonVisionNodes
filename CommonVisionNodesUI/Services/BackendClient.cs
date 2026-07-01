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
            using var messageStream = new MemoryStream();
            WebSocketReceiveResult result;
            WebSocketMessageType? messageType = null;
            do
            {
                // Execution messages can exceed a single WebSocket frame when previews are large,
                // so accumulate until EndOfMessage before deserializing.
                result = await socket.ReceiveAsync(buffer, cancellationToken);
                if (result.MessageType == WebSocketMessageType.Close)
                    return;

                messageType ??= result.MessageType;
                messageStream.Write(buffer, 0, result.Count);
            }
            while (!result.EndOfMessage);

            messageStream.Position = 0;
            var message = messageType == WebSocketMessageType.Binary
                ? DeserializeBinaryExecutionMessage(messageStream)
                : await JsonSerializer.DeserializeAsync<ExecutionMessageDto>(messageStream, _jsonOptions, cancellationToken);
            if (message is not null)
                await onMessage(message);
        }
    }

    private async Task<T?> ReadAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonSerializer.DeserializeAsync<T>(stream, _jsonOptions, cancellationToken);
    }

    private ExecutionMessageDto? DeserializeBinaryExecutionMessage(MemoryStream messageStream)
    {
        var payload = messageStream.ToArray();
        if (payload.Length < sizeof(int))
            return null;

        var metadataLength = BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(0, sizeof(int)));
        if (metadataLength <= 0 || metadataLength > payload.Length - sizeof(int))
            return null;

        var metadata = payload.AsSpan(sizeof(int), metadataLength);
        var imageBytes = payload[(sizeof(int) + metadataLength)..];
        var message = JsonSerializer.Deserialize<ExecutionMessageDto>(metadata, _jsonOptions);

        if (message is not null && TryGetImagePreview(message, out var imagePreview))
            imagePreview.BinaryData = imageBytes;

        return message;
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
}
