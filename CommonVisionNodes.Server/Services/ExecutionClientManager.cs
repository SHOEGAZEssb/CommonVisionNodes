using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text.Json;
using System.Text.Json.Serialization;
using CommonVisionNodes.Contracts;
using CommonVisionNodes.Runtime.Execution;

namespace CommonVisionNodes.Server.Services;

public sealed class ExecutionClientManager
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly ConcurrentDictionary<string, ClientSession> _sessions = new(StringComparer.OrdinalIgnoreCase);
    private readonly RuntimeGraphFactory _graphFactory;
    private readonly RuntimePreviewFactory _previewFactory;

    static ExecutionClientManager()
    {
        JsonOptions.Converters.Add(new JsonStringEnumConverter());
    }

    public ExecutionClientManager(RuntimeGraphFactory graphFactory, RuntimePreviewFactory previewFactory)
    {
        _graphFactory = graphFactory;
        _previewFactory = previewFactory;
    }

    public async Task<ExecutionAcceptedDto> StartExecutionAsync(ExecutionRequestDto request, CancellationToken cancellationToken)
    {
        var session = GetSession(request.ClientId);
        GraphExecutionRunner? previousRunner;

        lock (session.RunnerSync)
        {
            previousRunner = session.Runner;
            session.Runner = null;
        }

        if (previousRunner is not null)
            await previousRunner.DisposeAsync().ConfigureAwait(false);

        var runner = new GraphExecutionRunner(
            request,
            _graphFactory,
            _previewFactory,
            (message, publishCancellationToken) => BroadcastAsync(request.ClientId, message, publishCancellationToken),
            completedRunner => OnRunnerCompleted(request.ClientId, completedRunner));

        lock (session.RunnerSync)
            session.Runner = runner;

        runner.Start();

        return new ExecutionAcceptedDto
        {
            ClientId = request.ClientId,
            ExecutionId = runner.ExecutionId,
            Status = ExecutionStatusDto.Starting
        };
    }

    public async Task StopExecutionAsync(string clientId)
    {
        var session = GetSession(clientId);
        GraphExecutionRunner? runner;

        lock (session.RunnerSync)
        {
            runner = session.Runner;
            session.Runner = null;
        }

        if (runner is not null)
            await runner.DisposeAsync().ConfigureAwait(false);
    }

    public async Task AttachSocketAsync(string clientId, WebSocket socket, CancellationToken cancellationToken)
    {
        var session = GetSession(clientId);
        var socketId = Guid.NewGuid();
        session.Sockets[socketId] = socket;

        try
        {
            await SendAsync(socket, BuildIdleMessage(clientId), cancellationToken).ConfigureAwait(false);

            var buffer = new byte[1024];
            while (socket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
            {
                var result = await socket.ReceiveAsync(buffer, cancellationToken).ConfigureAwait(false);
                if (result.MessageType == WebSocketMessageType.Close)
                    break;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Client disconnected.
        }
        finally
        {
            session.Sockets.TryRemove(socketId, out _);

            if (socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
            {
                try
                {
                    await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed", CancellationToken.None).ConfigureAwait(false);
                }
                catch
                {
                    // Ignore disconnect cleanup failures.
                }
            }
        }
    }

    private async Task BroadcastAsync(string clientId, ExecutionMessageDto message, CancellationToken cancellationToken)
    {
        if (!_sessions.TryGetValue(clientId, out var session))
            return;

        await session.SendGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            foreach (var socketEntry in session.Sockets.ToArray())
            {
                if (socketEntry.Value.State != WebSocketState.Open)
                {
                    session.Sockets.TryRemove(socketEntry.Key, out _);
                    continue;
                }

                try
                {
                    await SendAsync(socketEntry.Value, message, cancellationToken).ConfigureAwait(false);
                }
                catch
                {
                    session.Sockets.TryRemove(socketEntry.Key, out _);
                }
            }
        }
        finally
        {
            session.SendGate.Release();
        }
    }

    private static ExecutionMessageDto BuildIdleMessage(string clientId)
        => new()
        {
            MessageType = ExecutionMessageTypeDto.ExecutionState,
            ExecutionState = new ExecutionStateDto
            {
                ClientId = clientId,
                ExecutionId = string.Empty,
                Status = ExecutionStatusDto.Idle,
                Message = "WebSocket connected.",
                TimestampUtc = DateTimeOffset.UtcNow
            }
        };

    private static Task SendAsync(WebSocket socket, ExecutionMessageDto message, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(message, JsonOptions);
        return socket.SendAsync(payload, WebSocketMessageType.Text, endOfMessage: true, cancellationToken);
    }

    private void OnRunnerCompleted(string clientId, GraphExecutionRunner completedRunner)
    {
        if (!_sessions.TryGetValue(clientId, out var session))
            return;

        lock (session.RunnerSync)
        {
            if (ReferenceEquals(session.Runner, completedRunner))
                session.Runner = null;
        }
    }

    private ClientSession GetSession(string clientId)
        => _sessions.GetOrAdd(clientId, _ => new ClientSession());

    private sealed class ClientSession
    {
        public ConcurrentDictionary<Guid, WebSocket> Sockets { get; } = [];

        public SemaphoreSlim SendGate { get; } = new(1, 1);

        public object RunnerSync { get; } = new();

        public GraphExecutionRunner? Runner { get; set; }
    }
}
