using System.Collections.Concurrent;
using CommonVisionNodes.Contracts;
using CommonVisionNodes.Runtime;
using CommonVisionNodes.Runtime.Definitions;
using CommonVisionNodes.Runtime.Execution;

namespace CommonVisionNodesUI.Services;

/// <summary>
/// Executes graph requests in the desktop process without an HTTP or WebSocket transport.
/// </summary>
/// <remarks>
/// The browser implementation of <see cref="IBackendClient"/> remains <see cref="BackendClient"/>.
/// This implementation deliberately awaits the listener callback for every message. In particular,
/// an image callback completes only after the UI has copied the raw preview bytes, which preserves
/// the runtime preview buffer ownership guarantees without WebSocket acknowledgements.
/// </remarks>
public sealed class DesktopBackendClient : IBackendClient, IAsyncDisposable
{
	private readonly RuntimeNodeCatalog _nodeCatalog = new();
	private readonly RuntimeGraphFactory _graphFactory;
	private readonly RuntimeCodeGenerationService _codeGenerationService;
	private readonly ConcurrentDictionary<string, ClientSession> _sessions = new(StringComparer.OrdinalIgnoreCase);

	/// <summary>
	/// Creates the in-process desktop execution client.
	/// </summary>
	public DesktopBackendClient()
	{
		_graphFactory = new RuntimeGraphFactory(_nodeCatalog);
		_codeGenerationService = new RuntimeCodeGenerationService(_graphFactory);
	}

	/// <inheritdoc/>
	public Task<IReadOnlyList<NodeDefinitionDto>> GetNodeDefinitionsAsync(CancellationToken cancellationToken = default)
		=> Task.FromResult<IReadOnlyList<NodeDefinitionDto>>(_nodeCatalog.GetDefinitions());

	/// <inheritdoc/>
	public Task<PathPickerResultDto> PickPathAsync(PathPickerRequestDto request, CancellationToken cancellationToken = default)
		=> throw new PlatformNotSupportedException(
			"Desktop path selection is handled directly by the Uno file pickers and does not use the execution client.");

	/// <inheritdoc/>
	public async Task<ExecutionAcceptedDto> ExecuteAsync(ExecutionRequestDto request, CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(request.ClientId);
		var session = _sessions.GetOrAdd(request.ClientId, static _ => new ClientSession());

		await session.RunnerTransitionGate.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
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
				(message, publishCancellationToken) => PublishAsync(request.ClientId, message, publishCancellationToken),
				completedRunner => OnRunnerCompleted(request.ClientId, completedRunner),
				() => HasExecutionListener(request.ClientId));

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

	/// <inheritdoc/>
	public async Task StopAsync(string clientId, CancellationToken cancellationToken = default)
	{
		if (!_sessions.TryGetValue(clientId, out var session))
			return;

		await session.RunnerTransitionGate.WaitAsync(cancellationToken).ConfigureAwait(false);
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

	/// <inheritdoc/>
	public Task TriggerNodeAsync(TriggerNodeRequestDto request, CancellationToken cancellationToken = default)
	{
		if (!TryGetTargetRunner(request.ClientId, executionId: null, out var runner))
			throw new InvalidOperationException("No active desktop execution exists for this client.");

		runner.TriggerManualNode(request.NodeId);
		return Task.CompletedTask;
	}

	/// <inheritdoc/>
	public Task UpdateExecutionSettingsAsync(UpdateExecutionSettingsRequestDto request, CancellationToken cancellationToken = default)
	{
		if (!TryGetTargetRunner(request.ClientId, request.ExecutionId, out var runner))
			throw new InvalidOperationException("The desktop execution is no longer active.");

		runner.UpdatePreviewSettings(request.PreviewRefreshRate, request.PreviewImageMaxDimension);
		return Task.CompletedTask;
	}

	/// <inheritdoc/>
	public Task UpdateNodePropertiesAsync(UpdateNodePropertiesRequestDto request, CancellationToken cancellationToken = default)
	{
		if (!TryGetTargetRunner(request.ClientId, request.ExecutionId, out var runner) ||
			!runner.UpdateNodeProperties(request.NodeId, request.Properties))
		{
			throw new InvalidOperationException("The desktop execution could not apply the requested node properties.");
		}

		return Task.CompletedTask;
	}

	/// <inheritdoc/>
	public Task<string> GenerateCodeAsync(GraphDto graph, CancellationToken cancellationToken = default)
		=> Task.FromResult(_codeGenerationService.GenerateCode(graph));

	/// <inheritdoc/>
	public async Task ListenAsync(
		string clientId,
		Func<ExecutionMessageDto, Task> onMessage,
		CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
		ArgumentNullException.ThrowIfNull(onMessage);

		var session = _sessions.GetOrAdd(clientId, static _ => new ClientSession());
		lock (session.ListenerSync)
		{
			if (session.Listener is not null)
				throw new InvalidOperationException("Only one desktop execution listener is supported per client.");

			session.Listener = onMessage;
		}

		try
		{
			await onMessage(BuildIdleMessage(clientId)).ConfigureAwait(false);
			await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			// Listener lifetime is controlled by the UI view model.
		}
		finally
		{
			lock (session.ListenerSync)
			{
				if (ReferenceEquals(session.Listener, onMessage))
					session.Listener = null;
			}

		}
	}

	/// <inheritdoc/>
	public async ValueTask DisposeAsync()
	{
		foreach (var pair in _sessions.ToArray())
		{
			var session = pair.Value;
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
	}

	private async Task PublishAsync(string clientId, ExecutionMessageDto message, CancellationToken cancellationToken)
	{
		if (!_sessions.TryGetValue(clientId, out var session))
			return;

		await session.SendGate.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			Func<ExecutionMessageDto, Task>? listener;
			lock (session.ListenerSync)
				listener = session.Listener;

			if (listener is not null)
				await listener(message).ConfigureAwait(false);
		}
		finally
		{
			session.SendGate.Release();
		}
	}

	private bool TryGetTargetRunner(string clientId, string? executionId, out GraphExecutionRunner runner)
	{
		runner = null!;
		if (!_sessions.TryGetValue(clientId, out var session))
			return false;

		lock (session.RunnerSync)
		{
			if (session.Runner is null ||
				(!string.IsNullOrWhiteSpace(executionId) &&
				 !string.Equals(executionId, session.Runner.ExecutionId, StringComparison.OrdinalIgnoreCase)))
			{
				return false;
			}

			runner = session.Runner;
			return true;
		}
	}

	private bool HasExecutionListener(string clientId)
	{
		if (!_sessions.TryGetValue(clientId, out var session))
			return false;

		lock (session.ListenerSync)
			return session.Listener is not null;
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

	private static ExecutionMessageDto BuildIdleMessage(string clientId)
		=> new()
		{
			MessageType = ExecutionMessageTypeDto.ExecutionState,
			ExecutionState = new ExecutionStateDto
			{
				ClientId = clientId,
				ExecutionId = string.Empty,
				Status = ExecutionStatusDto.Idle,
				Message = "Desktop execution listener connected.",
				TimestampUtc = DateTimeOffset.UtcNow
			}
		};

	private sealed class ClientSession
	{
		public object ListenerSync { get; } = new();

		public object RunnerSync { get; } = new();

		public SemaphoreSlim RunnerTransitionGate { get; } = new(1, 1);

		public SemaphoreSlim SendGate { get; } = new(1, 1);

		public Func<ExecutionMessageDto, Task>? Listener { get; set; }

		public GraphExecutionRunner? Runner { get; set; }
	}
}
