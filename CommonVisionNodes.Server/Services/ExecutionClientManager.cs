using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text.Json;
using System.Text.Json.Serialization;
using CommonVisionNodes.Contracts;
using CommonVisionNodes.Runtime.Execution;

namespace CommonVisionNodes.Server.Services;

/// <summary>
/// Tracks client execution sessions, active runners, and subscribed WebSocket connections.
/// </summary>
/// <remarks>
/// Creates an execution client manager.
/// </remarks>
/// <param name="graphFactory">Factory used by new execution runners.</param>
/// <param name="previewFactory">Preview factory used by new execution runners.</param>
public sealed class ExecutionClientManager(RuntimeGraphFactory graphFactory, RuntimePreviewFactory previewFactory)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly ConcurrentDictionary<string, ClientSession> _sessions = new(StringComparer.OrdinalIgnoreCase);
    private readonly RuntimeGraphFactory _graphFactory = graphFactory;
    private readonly RuntimePreviewFactory _previewFactory = previewFactory;

    static ExecutionClientManager()
    {
        JsonOptions.Converters.Add(new JsonStringEnumConverter());
    }

	/// <summary>
	/// Starts execution for a client, replacing any existing runner for that client.
	/// </summary>
	/// <param name="request">Execution request.</param>
	/// <param name="cancellationToken">Cancellation token for request processing.</param>
	/// <returns>Accepted execution metadata.</returns>
	public async Task<ExecutionAcceptedDto> StartExecutionAsync(ExecutionRequestDto request, CancellationToken cancellationToken)
    {
        var session = GetSession(request.ClientId);
        GraphExecutionRunner? previousRunner;

        lock (session.RunnerSync)
        {
            previousRunner = session.Runner;
            session.Runner = null;
        }

        // Dispose the previous runner outside the session lock. Disposal waits for graph shutdown
        // and may publish messages, so keeping the lock held here can block socket operations.
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

    /// <summary>
    /// Stops the active execution for a client.
    /// </summary>
    /// <param name="clientId">Client identifier.</param>
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

    /// <summary>
    /// Updates live preview settings for the active execution.
    /// </summary>
    /// <param name="request">Settings update request.</param>
    /// <returns><c>true</c> when a runner was found and updated.</returns>
    public bool UpdateExecutionSettings(UpdateExecutionSettingsRequestDto request)
    {
        var session = GetSession(request.ClientId);
        GraphExecutionRunner? runner;

        lock (session.RunnerSync)
            runner = session.Runner;

        if (runner is null)
            return false;

        runner.UpdatePreviewSettings(request.PreviewRefreshRate, request.PreviewImageMaxDimension);
        return true;
    }

    /// <summary>
    /// Queues a manual trigger for the active execution.
    /// </summary>
    /// <param name="request">Trigger request.</param>
    /// <returns><c>true</c> when a runner was found and the trigger was queued.</returns>
    public bool TriggerManualNode(TriggerNodeRequestDto request)
    {
        var session = GetSession(request.ClientId);
        GraphExecutionRunner? runner;

        lock (session.RunnerSync)
            runner = session.Runner;

        if (runner is null)
            return false;

        runner.TriggerManualNode(request.NodeId);
        return true;
    }

    /// <summary>
    /// Updates supported live node properties on the active execution.
    /// </summary>
    /// <param name="request">Node property update request.</param>
    /// <returns><c>true</c> when the runner accepted the property update.</returns>
    public bool UpdateNodeProperties(UpdateNodePropertiesRequestDto request)
    {
        var session = GetSession(request.ClientId);
        GraphExecutionRunner? runner;

        lock (session.RunnerSync)
            runner = session.Runner;

        return runner?.UpdateNodeProperties(request.NodeId, request.Properties) == true;
    }

    /// <summary>
    /// Attaches a WebSocket subscriber for execution messages from one client session.
    /// </summary>
    /// <param name="clientId">Client identifier to subscribe to.</param>
    /// <param name="socket">Accepted WebSocket.</param>
    /// <param name="cancellationToken">Cancellation token for socket lifetime.</param>
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
            // WebSocket sends are serialized per client session. ClientWebSocket instances do not
            // permit concurrent sends, and this also keeps message order stable for the UI.
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
