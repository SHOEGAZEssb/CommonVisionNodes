using System.IO;
using System.Net.WebSockets;
using System.Text.Json;
using System.Text.Json.Serialization;
using CommonVisionNodes.Contracts;

namespace CommonVisionNodesUI.Services;

public sealed class BackendClient : IBackendClient
{
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly Uri _webSocketUriBase;

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

    public async Task<IReadOnlyList<NodeDefinitionDto>> GetNodeDefinitionsAsync(CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.GetAsync("api/nodes/definitions", cancellationToken);
        response.EnsureSuccessStatusCode();
        return await ReadAsync<List<NodeDefinitionDto>>(response, cancellationToken) ?? [];
    }

    public async Task<ExecutionAcceptedDto> ExecuteAsync(ExecutionRequestDto request, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsJsonAsync("api/graph/execute", request, _jsonOptions, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await ReadAsync<ExecutionAcceptedDto>(response, cancellationToken) ?? new ExecutionAcceptedDto();
    }

    public async Task StopAsync(string clientId, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsJsonAsync(
            "api/graph/stop",
            new StopExecutionRequestDto { ClientId = clientId },
            _jsonOptions,
            cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<string> GenerateCodeAsync(GraphDto graph, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsJsonAsync("api/graph/codegen", graph, _jsonOptions, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

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
            do
            {
                result = await socket.ReceiveAsync(buffer, cancellationToken);
                if (result.MessageType == WebSocketMessageType.Close)
                    return;

                messageStream.Write(buffer, 0, result.Count);
            }
            while (!result.EndOfMessage);

            messageStream.Position = 0;
            var message = await JsonSerializer.DeserializeAsync<ExecutionMessageDto>(messageStream, _jsonOptions, cancellationToken);
            if (message is not null)
                await onMessage(message);
        }
    }

    private async Task<T?> ReadAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonSerializer.DeserializeAsync<T>(stream, _jsonOptions, cancellationToken);
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


