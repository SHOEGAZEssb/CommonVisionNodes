using System.IO;
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
    private const int WebSocketReceiveBufferSize = 64 * 1024;

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

        var buffer = new byte[WebSocketReceiveBufferSize];
        var imageBufferCache = new BinaryImageBufferCache();
        while (socket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
        {
            var result = await socket.ReceiveAsync(buffer, cancellationToken);
            if (result.MessageType == WebSocketMessageType.Close)
                return;

            var message = result.MessageType == WebSocketMessageType.Binary
                ? await DeserializeBinaryExecutionMessageAsync(socket, buffer, result, imageBufferCache, cancellationToken)
                : await DeserializeTextExecutionMessageAsync(socket, buffer, result, cancellationToken);
            if (message is not null)
            {
                if (message.ExecutionState?.Status == ExecutionStatusDto.Starting)
                    imageBufferCache.Clear();

                await onMessage(message);
            }
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
        BinaryImageBufferCache imageBufferCache,
        CancellationToken cancellationToken)
    {
        var builder = new BinaryExecutionMessageBuilder(_jsonOptions, imageBufferCache);
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

    private static Uri BuildWebSocketBaseUri(Uri httpBaseUri)
    {
        var builder = new UriBuilder(httpBaseUri)
        {
            Scheme = httpBaseUri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase) ? "wss" : "ws"
        };
        return builder.Uri;
    }

}
