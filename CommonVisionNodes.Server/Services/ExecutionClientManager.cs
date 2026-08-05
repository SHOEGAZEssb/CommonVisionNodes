using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text.Json;
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
public sealed class ExecutionClientManager(RuntimeGraphFactory graphFactory)
{
	private static readonly TimeSpan PreviewAcknowledgementTimeout = TimeSpan.FromSeconds(2);
	private static readonly ContractsJsonSerializerContext JsonContext = ContractsJsonSerializerContext.Default;

	private readonly ConcurrentDictionary<string, ClientSession> _sessions = new(StringComparer.OrdinalIgnoreCase);
	private readonly RuntimeGraphFactory _graphFactory = graphFactory;

	internal int SessionCount => _sessions.Count;

	/// <summary>
	/// Starts execution for a client, replacing any existing runner for that client.
	/// </summary>
	/// <param name="request">Execution request.</param>
	/// <param name="cancellationToken">Cancellation token for request processing.</param>
	/// <returns>Accepted execution metadata.</returns>
	public async Task<ExecutionAcceptedDto> StartExecutionAsync(ExecutionRequestDto request, CancellationToken cancellationToken)
	{
		using var sessionLease = AcquireSession(request.ClientId);
		var session = sessionLease.Session;

		await session.RunnerTransitionGate.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
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
				(message, publishCancellationToken) => BroadcastAsync(request.ClientId, message, publishCancellationToken),
				completedRunner => OnRunnerCompleted(request.ClientId, completedRunner),
				() => HasOpenSockets(request.ClientId));

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
		finally
		{
			session.RunnerTransitionGate.Release();
		}
	}

	/// <summary>
	/// Stops the active execution for a client.
	/// </summary>
	/// <param name="clientId">Client identifier.</param>
	public async Task StopExecutionAsync(string clientId)
	{
		using var sessionLease = AcquireSession(clientId);
		var session = sessionLease.Session;

		await session.RunnerTransitionGate.WaitAsync().ConfigureAwait(false);
		try
		{
			GraphExecutionRunner? runner;

			lock (session.RunnerSync)
			{
				runner = session.Runner;
				session.Runner = null;
			}

			if (runner is not null)
				await runner.DisposeAsync().ConfigureAwait(false);
		}
		finally
		{
			session.RunnerTransitionGate.Release();
		}
	}

	/// <summary>
	/// Updates live preview settings for the active execution.
	/// </summary>
	/// <param name="request">Settings update request.</param>
	/// <returns><c>true</c> when a runner was found and updated.</returns>
	public bool UpdateExecutionSettings(UpdateExecutionSettingsRequestDto request)
	{
		using var sessionLease = AcquireSession(request.ClientId);
		var session = sessionLease.Session;
		GraphExecutionRunner? runner;

		lock (session.RunnerSync)
			runner = session.Runner;

		if (runner is null || !TargetsRunner(request.ExecutionId, runner))
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
		using var sessionLease = AcquireSession(request.ClientId);
		var session = sessionLease.Session;
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
		using var sessionLease = AcquireSession(request.ClientId);
		var session = sessionLease.Session;
		GraphExecutionRunner? runner;

		lock (session.RunnerSync)
			runner = session.Runner;

		if (runner is null || !TargetsRunner(request.ExecutionId, runner))
			return false;

		return runner.UpdateNodeProperties(request.NodeId, request.Properties);
	}

	/// <summary>
	/// Attaches a WebSocket subscriber for execution messages from one client session.
	/// </summary>
	/// <param name="clientId">Client identifier to subscribe to.</param>
	/// <param name="socket">Accepted WebSocket.</param>
	/// <param name="cancellationToken">Cancellation token for socket lifetime.</param>
	public async Task AttachSocketAsync(string clientId, WebSocket socket, CancellationToken cancellationToken)
	{
		using var sessionLease = AcquireSession(clientId);
		var session = sessionLease.Session;
		var socketId = Guid.NewGuid();
		var socketState = new ClientSocketState(socket);
		session.Sockets[socketId] = socketState;

		try
		{
			await SendAsync(socket, BuildIdleMessage(clientId), cancellationToken).ConfigureAwait(false);

			var buffer = new byte[1024];
			while (socket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
			{
				var result = await socket.ReceiveAsync(buffer, cancellationToken).ConfigureAwait(false);
				if (result.MessageType == WebSocketMessageType.Close)
					break;

				await ProcessClientMessageAsync(socketState, buffer, result, cancellationToken).ConfigureAwait(false);
			}
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			// Client disconnected.
		}
		finally
		{
			session.Sockets.TryRemove(socketId, out _);
			socketState.Acknowledgements.CancelPending();

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

		List<(ClientSocketState SocketState, Task AcknowledgementTask)>? acknowledgementWaits = null;
		await session.SendGate.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			// WebSocket sends are serialized per client session. ClientWebSocket instances do not
			// permit concurrent sends, and this also keeps message order stable for the UI.
			foreach (var socketEntry in session.Sockets.ToArray())
			{
				if (socketEntry.Value.Socket.State != WebSocketState.Open)
				{
					session.Sockets.TryRemove(socketEntry.Key, out _);
					socketEntry.Value.Acknowledgements.CancelPending();
					continue;
				}

				try
				{
					var acknowledgementTask = socketEntry.Value.Acknowledgements.Begin(message);
					await SendAsync(socketEntry.Value.Socket, message, cancellationToken).ConfigureAwait(false);
					if (acknowledgementTask is not null)
					{
						acknowledgementWaits ??= [];
						acknowledgementWaits.Add((socketEntry.Value, acknowledgementTask));
					}
				}
				catch
				{
					session.Sockets.TryRemove(socketEntry.Key, out _);
					socketEntry.Value.Acknowledgements.CancelPending();
				}
			}
		}
		finally
		{
			session.SendGate.Release();
		}

		if (acknowledgementWaits is null)
			return;

		// The send gate protects WebSocket.SendAsync only. Waiting outside it lets execution
		// telemetry and state messages proceed while the browser applies an image frame.
		foreach (var (SocketState, AcknowledgementTask) in acknowledgementWaits)
		{
			await WaitForPreviewAcknowledgementAsync(
					SocketState,
					AcknowledgementTask,
					cancellationToken)
				.ConfigureAwait(false);
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
		if (BinaryExecutionMessageCodec.TryGetImagePreview(message, out var imagePreview))
		{
			var imageBytes = BinaryExecutionMessageCodec.GetImageBytes(imagePreview);
			if (imageBytes is { Length: > 0 })
				return SendBinaryPayloadAsync(socket, message, imageBytes, cancellationToken);
		}

		var payload = JsonSerializer.SerializeToUtf8Bytes(message, JsonContext.ExecutionMessageDto);
		return socket.SendAsync(payload, WebSocketMessageType.Text, endOfMessage: true, cancellationToken);
	}

	private static async Task WaitForPreviewAcknowledgementAsync(
		ClientSocketState socketState,
		Task acknowledgementTask,
		CancellationToken cancellationToken)
	{
		try
		{
			await acknowledgementTask
				.WaitAsync(PreviewAcknowledgementTimeout, cancellationToken)
				.ConfigureAwait(false);
		}
		catch (TimeoutException)
		{
			// Do not let a backgrounded or overloaded browser stop graph execution indefinitely.
			// The per-node publisher remains bounded and can try a fresher frame on the next tick.
		}
		catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
		{
			// The socket disconnected or disabled acknowledgements while this preview was pending.
		}
		finally
		{
			socketState.Acknowledgements.CancelPending();
		}
	}

	private static async Task ProcessClientMessageAsync(
		ClientSocketState socketState,
		byte[] buffer,
		WebSocketReceiveResult firstResult,
		CancellationToken cancellationToken)
	{
		if (firstResult.MessageType != WebSocketMessageType.Text)
		{
			await DrainClientMessageAsync(socketState.Socket, buffer, firstResult, cancellationToken).ConfigureAwait(false);
			return;
		}

		using var messageStream = new MemoryStream();
		var result = firstResult;
		while (true)
		{
			messageStream.Write(buffer, 0, result.Count);
			if (result.EndOfMessage)
				break;

			result = await socketState.Socket.ReceiveAsync(buffer, cancellationToken).ConfigureAwait(false);
			if (result.MessageType == WebSocketMessageType.Close)
				return;
		}

		try
		{
			var message = JsonSerializer.Deserialize(
				messageStream.GetBuffer().AsSpan(0, checked((int)messageStream.Length)),
				JsonContext.PreviewClientMessageDto);
			switch (message?.MessageType)
			{
				case PreviewClientMessageTypeDto.Configure:
					socketState.Acknowledgements.Configure(message.SupportsAcknowledgements);
					break;
				case PreviewClientMessageTypeDto.Acknowledge:
					socketState.Acknowledgements.TryAcknowledge(message);
					break;
			}
		}
		catch (JsonException)
		{
			// Ignore unknown client messages so an invalid control packet cannot end the socket.
		}
	}

	private static async Task DrainClientMessageAsync(
		WebSocket socket,
		byte[] buffer,
		WebSocketReceiveResult firstResult,
		CancellationToken cancellationToken)
	{
		var result = firstResult;
		while (!result.EndOfMessage)
		{
			result = await socket.ReceiveAsync(buffer, cancellationToken).ConfigureAwait(false);
			if (result.MessageType == WebSocketMessageType.Close)
				return;
		}
	}

	private static async Task SendBinaryPayloadAsync(WebSocket socket, ExecutionMessageDto message, byte[] imageBytes, CancellationToken cancellationToken)
	{
		var metadataBytes = BinaryExecutionMessageCodec.SerializeMetadata(message, JsonContext.ExecutionMessageDto);
		var metadataLengthHeader = BinaryExecutionMessageCodec.CreateMetadataLengthHeader(metadataBytes.Length);

		await socket.SendAsync(metadataLengthHeader, WebSocketMessageType.Binary, endOfMessage: false, cancellationToken).ConfigureAwait(false);
		await socket.SendAsync(metadataBytes, WebSocketMessageType.Binary, endOfMessage: false, cancellationToken).ConfigureAwait(false);
		await socket.SendAsync(imageBytes, WebSocketMessageType.Binary, endOfMessage: true, cancellationToken).ConfigureAwait(false);
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

		TryRemoveIdleSession(clientId, session);
	}

	private SessionLease AcquireSession(string clientId)
	{
		while (true)
		{
			var session = _sessions.GetOrAdd(clientId, static _ => new ClientSession());

			lock (session.LifetimeSync)
			{
				if (!_sessions.TryGetValue(clientId, out var currentSession) ||
					!ReferenceEquals(session, currentSession))
				{
					continue;
				}

				session.ActiveLeaseCount++;
				return new SessionLease(this, clientId, session);
			}
		}
	}

	private void ReleaseSession(string clientId, ClientSession session)
	{
		lock (session.LifetimeSync)
			session.ActiveLeaseCount--;

		TryRemoveIdleSession(clientId, session);
	}

	private void TryRemoveIdleSession(string clientId, ClientSession session)
	{
		lock (session.LifetimeSync)
		{
			if (session.ActiveLeaseCount != 0 || !session.Sockets.IsEmpty)
				return;

			lock (session.RunnerSync)
			{
				if (session.Runner is not null)
					return;
			}

			var removed = ((ICollection<KeyValuePair<string, ClientSession>>)_sessions)
				.Remove(new KeyValuePair<string, ClientSession>(clientId, session));

			if (removed)
				session.Dispose();
		}
	}

	private static bool TargetsRunner(string? executionId, GraphExecutionRunner runner)
		=> string.IsNullOrWhiteSpace(executionId) ||
			string.Equals(executionId, runner.ExecutionId, StringComparison.OrdinalIgnoreCase);

	private bool HasOpenSockets(string clientId)
	{
		return _sessions.TryGetValue(clientId, out var session) &&
			session.Sockets.Values.Any(socket => socket.Socket.State == WebSocketState.Open);
	}

	private sealed class ClientSession : IDisposable
	{
		public object LifetimeSync { get; } = new();

		public int ActiveLeaseCount { get; set; }

		public ConcurrentDictionary<Guid, ClientSocketState> Sockets { get; } = [];

		public SemaphoreSlim SendGate { get; } = new(1, 1);

		public SemaphoreSlim RunnerTransitionGate { get; } = new(1, 1);

		public object RunnerSync { get; } = new();

		public GraphExecutionRunner? Runner { get; set; }

		public void Dispose()
		{
			SendGate.Dispose();
			RunnerTransitionGate.Dispose();
		}
	}

	private sealed class ClientSocketState(WebSocket socket)
	{
		public WebSocket Socket { get; } = socket;

		public PreviewAcknowledgementGate Acknowledgements { get; } = new();
	}

	private sealed class SessionLease(
		ExecutionClientManager owner,
		string clientId,
		ClientSession session) : IDisposable
	{
		private ExecutionClientManager? _owner = owner;

		public ClientSession Session { get; } = session;

		public void Dispose()
		{
			var currentOwner = Interlocked.Exchange(ref _owner, null);
			currentOwner?.ReleaseSession(clientId, Session);
		}
	}
}
