using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Channels;
using CommonVisionNodes.Contracts;

namespace CommonVisionNodes.Runtime.Execution;

/// <summary>
/// Owns one graph execution, publishes progress messages, and coordinates live updates.
/// </summary>
/// <remarks>
/// Creates a graph execution runner.
/// </remarks>
/// <param name="request">Execution request containing graph, client id, and execution settings.</param>
/// <param name="graphFactory">Factory used to build the runtime graph.</param>
/// <param name="publishAsync">Callback used to publish execution messages.</param>
/// <param name="onCompleted">Callback invoked after the runner exits.</param>
public sealed class GraphExecutionRunner(
	ExecutionRequestDto request,
	RuntimeGraphFactory graphFactory,
	Func<ExecutionMessageDto, CancellationToken, Task> publishAsync,
	Action<GraphExecutionRunner> onCompleted,
	Func<bool>? hasPreviewSubscribers = null) : IAsyncDisposable
{
	private const double ContinuousTelemetryIntervalMilliseconds = 100.0;
	private static readonly TimeSpan MaximumIdleDelay = TimeSpan.FromMilliseconds(50);
	private static readonly TimeSpan ManualTriggerPollingDelay = TimeSpan.FromMilliseconds(10);
	private readonly ExecutionRequestDto _request = request;
	private readonly RuntimeGraphFactory _graphFactory = graphFactory;
	private readonly Func<ExecutionMessageDto, CancellationToken, Task> _publishAsync = publishAsync;
	private readonly Action<GraphExecutionRunner> _onCompleted = onCompleted;
	private readonly Func<bool> _hasPreviewSubscribers = hasPreviewSubscribers ?? (() => true);
	private readonly HashSet<string> _previewEnabledNodeIds = request.Graph.Nodes
			.Where(node => !string.IsNullOrWhiteSpace(node.Id) && NodePreviewSettings.IsEnabled(node.Type, node.Properties))
			.Select(node => node.Id)
			.ToHashSet(StringComparer.OrdinalIgnoreCase);
	private readonly Lock _previewSync = new();
	private readonly BinaryImageBufferCache _previewImageBufferCache = new();
	private readonly ConcurrentDictionary<string, byte> _previewsInFlight = new(StringComparer.OrdinalIgnoreCase);
	private readonly Channel<QueuedPreview> _previewPublicationQueue = Channel.CreateUnbounded<QueuedPreview>(
		new UnboundedChannelOptions
		{
			SingleReader = true,
			SingleWriter = true,
			AllowSynchronousContinuations = false
		});
	private readonly Lock _manualTriggerSync = new();
	private readonly Dictionary<string, int> _manualTriggerCounts = new(StringComparer.OrdinalIgnoreCase);
	private readonly Lock _graphSync = new();
	private readonly CancellationTokenSource _cts = new();
	private int _previewRefreshRate = request.PreviewRefreshRate;
	private int _previewImageMaxDimension = request.PreviewImageMaxDimension;
	private long _previewSequence;
	private Task? _executionTask;
	private Task? _previewPublicationTask;
	private RuntimeGraphBuildResult? _activeGraphBuildResult;

	/// <summary>
	/// Runtime execution identifier assigned to this runner.
	/// </summary>
	public string ExecutionId { get; } = Guid.NewGuid().ToString("N");

	/// <summary>
	/// Starts execution on a background task. Subsequent calls are ignored.
	/// </summary>
	public void Start()
	{
		if (_executionTask is not null)
			return;

		if (_request.Mode == ExecutionModeDto.Continuous)
			_previewPublicationTask = Task.Run(() => PublishQueuedPreviewsAsync(_cts.Token));

		_executionTask = Task.Run(() => RunAsync(_cts.Token));
	}

	/// <summary>
	/// Queues one manual trigger event for a running manual trigger node.
	/// </summary>
	/// <param name="nodeId">Serialized node id of the manual trigger node.</param>
	public void TriggerManualNode(string nodeId)
	{
		if (string.IsNullOrWhiteSpace(nodeId))
			return;

		lock (_manualTriggerSync)
		{
			_manualTriggerCounts.TryGetValue(nodeId, out var count);
			_manualTriggerCounts[nodeId] = count == int.MaxValue ? count : count + 1;
		}
	}

	/// <summary>
	/// Updates preview publication settings for a running continuous execution.
	/// </summary>
	/// <param name="previewRefreshRate">Preview refresh rate in frames per second. A value of 1001 is treated as unlimited.</param>
	/// <param name="previewImageMaxDimension">Maximum preview long edge, or 0 for full resolution.</param>
	public void UpdatePreviewSettings(int previewRefreshRate, int previewImageMaxDimension)
	{
		Volatile.Write(ref _previewRefreshRate, Math.Clamp(previewRefreshRate, 1, 1001));
		Volatile.Write(ref _previewImageMaxDimension, Math.Max(0, previewImageMaxDimension));
	}

	/// <summary>
	/// Updates live-editable node properties on the active runtime graph.
	/// </summary>
	/// <param name="nodeId">Serialized node id to update.</param>
	/// <param name="properties">Replacement property values.</param>
	/// <returns><c>true</c> when the update was applied.</returns>
	public bool UpdateNodeProperties(string nodeId, IEnumerable<NodePropertyDto> properties)
	{
		if (string.IsNullOrWhiteSpace(nodeId))
			return false;

		lock (_graphSync)
		{
			if (_activeGraphBuildResult is null ||
				!_activeGraphBuildResult.NodesById.TryGetValue(nodeId, out var node))
			{
				return false;
			}

			var propertyList = properties.ToList();
			var previewUpdated = TryUpdatePreviewEnabled(nodeId, propertyList);
			var liveProperties = GetLiveProperties(node, propertyList).ToList();

			if (liveProperties.Count == 0)
				return previewUpdated;

			RuntimeNodePropertyBinder.Apply(node, liveProperties);
			return true;
		}
	}

	/// <summary>
	/// Requests execution stop and waits for the background task to exit.
	/// </summary>
	public async Task StopAsync()
	{
		if (_cts.IsCancellationRequested)
		{
			if (_executionTask is not null)
				await _executionTask.ConfigureAwait(false);
			return;
		}

		_cts.Cancel();

		if (_executionTask is not null)
			await _executionTask.ConfigureAwait(false);
	}

	/// <summary>
	/// Stops execution and releases runner resources.
	/// </summary>
	public async ValueTask DisposeAsync()
	{
		await StopAsync().ConfigureAwait(false);
		_cts.Dispose();
	}

	private async Task RunAsync(CancellationToken cancellationToken)
	{
		RuntimeGraphBuildResult? graphBuildResult = null;
		var framesProcessed = 0L;
		var previewTimer = Stopwatch.StartNew();
		var fpsTimer = Stopwatch.StartNew();
		var telemetryTimer = Stopwatch.StartNew();
		var framesInWindow = 0;

		try
		{
			await PublishStateAsync(ExecutionStatusDto.Starting, "Building execution graph.", framesProcessed, null, null, ExecutionMessageTypeDto.ExecutionState, cancellationToken).ConfigureAwait(false);

			graphBuildResult = _graphFactory.Build(_request.Graph);
			ConfigureManualTriggerNodes(graphBuildResult);
			lock (_graphSync)
				_activeGraphBuildResult = graphBuildResult;

			await PublishStateAsync(ExecutionStatusDto.Initializing, "Initializing runtime nodes.", framesProcessed, null, null, ExecutionMessageTypeDto.ExecutionState, cancellationToken).ConfigureAwait(false);
			lock (_graphSync)
				graphBuildResult.Graph.Initialize();

			await PublishStateAsync(ExecutionStatusDto.Running, "Execution started.", framesProcessed, null, null, ExecutionMessageTypeDto.ExecutionState, cancellationToken).ConfigureAwait(false);

			if (_request.Mode == ExecutionModeDto.Single)
			{
				var (elapsed, _) = await ExecuteFrameAsync(graphBuildResult, publishNodeUpdates: true, cancellationToken).ConfigureAwait(false);
				framesProcessed = 1;
				await PublishPreviewsAsync(graphBuildResult, cancellationToken).ConfigureAwait(false);
				await PublishStateAsync(ExecutionStatusDto.Completed, "Execution completed.", framesProcessed, null, elapsed.TotalMilliseconds, ExecutionMessageTypeDto.Completed, cancellationToken).ConfigureAwait(false);
				return;
			}

			while (!cancellationToken.IsCancellationRequested)
			{
				var (elapsed, executedWork) = await ExecuteFrameAsync(graphBuildResult, publishNodeUpdates: false, cancellationToken).ConfigureAwait(false);
				if (executedWork)
				{
					framesProcessed++;
					framesInWindow++;
				}

				double? fps = null;
				if (fpsTimer.ElapsedMilliseconds >= 1000)
				{
					fps = framesInWindow * 1000.0 / fpsTimer.ElapsedMilliseconds;
					framesInWindow = 0;
					fpsTimer.Restart();
				}

				var shouldPublishTelemetry = telemetryTimer.Elapsed.TotalMilliseconds >= ContinuousTelemetryIntervalMilliseconds;
				if (shouldPublishTelemetry)
				{
					await PublishNodeUpdatesAsync(graphBuildResult, cancellationToken).ConfigureAwait(false);
					await PublishStateAsync(ExecutionStatusDto.Running, "Executing.", framesProcessed, fps, elapsed.TotalMilliseconds, ExecutionMessageTypeDto.ExecutionState, cancellationToken).ConfigureAwait(false);
					telemetryTimer.Restart();
				}

				var previewIntervalMs = GetPreviewIntervalMilliseconds(Volatile.Read(ref _previewRefreshRate));
				if (executedWork && (previewIntervalMs == 0 || previewTimer.Elapsed.TotalMilliseconds >= previewIntervalMs))
				{
					// Preview generation can be expensive, especially with PNG fallbacks, so it is
					// throttled independently from the graph execution loop.
					await PublishPreviewsAsync(graphBuildResult, cancellationToken).ConfigureAwait(false);
					previewTimer.Restart();
				}

				if (!executedWork)
					await DelayUntilNextTriggerAsync(graphBuildResult.Graph, cancellationToken).ConfigureAwait(false);
			}
		}
		catch (OperationCanceledException) when (_cts.IsCancellationRequested)
		{
			await PublishStateAsync(ExecutionStatusDto.Stopped, "Execution stopped.", framesProcessed, null, null, ExecutionMessageTypeDto.ExecutionState, CancellationToken.None).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			await PublishFailureAsync(ex, graphBuildResult, framesProcessed).ConfigureAwait(false);
		}
		finally
		{
			_previewPublicationQueue.Writer.TryComplete();
			if (_previewPublicationTask is not null)
			{
				try
				{
					await _previewPublicationTask.ConfigureAwait(false);
				}
				catch (OperationCanceledException) when (_cts.IsCancellationRequested)
				{
					// Stopping an execution abandons any preview currently being transmitted.
				}
			}

			lock (_graphSync)
			{
				if (ReferenceEquals(_activeGraphBuildResult, graphBuildResult))
					_activeGraphBuildResult = null;
			}

			graphBuildResult?.Dispose();
			_onCompleted(this);
		}
	}

	private async Task<(TimeSpan Elapsed, bool ExecutedWork)> ExecuteFrameAsync(RuntimeGraphBuildResult graphBuildResult, bool publishNodeUpdates, CancellationToken cancellationToken)
	{
		var executionTimer = Stopwatch.StartNew();
		bool executedWork;

		try
		{
			lock (_graphSync)
				executedWork = graphBuildResult.Graph.ExecuteWithActivity();
		}
		catch (NodeExecutionException nodeExecutionException)
		{
			executionTimer.Stop();

			if (graphBuildResult.NodeIdsByRuntime.TryGetValue(nodeExecutionException.Node, out var nodeId))
			{
				await PublishNodeUpdateAsync(
					nodeId,
					nodeExecutionException.Node,
					NodeExecutionStatusDto.Failed,
					nodeExecutionException.InnerException?.Message ?? nodeExecutionException.Message,
					CancellationToken.None).ConfigureAwait(false);
			}

			throw;
		}

		executionTimer.Stop();

		if (publishNodeUpdates)
			await PublishNodeUpdatesAsync(graphBuildResult, cancellationToken).ConfigureAwait(false);

		return (executionTimer.Elapsed, executedWork);
	}

	private static Task DelayUntilNextTriggerAsync(NodeGraph graph, CancellationToken cancellationToken)
	{
		var nextTimeTriggerDelay = graph.Nodes
			.OfType<TimeTriggerNode>()
			.Select(trigger => trigger.GetDelayUntilNextTrigger())
			.Where(delay => delay > TimeSpan.Zero)
			.DefaultIfEmpty(ManualTriggerPollingDelay)
			.Min();
		var delay = nextTimeTriggerDelay < MaximumIdleDelay
			? nextTimeTriggerDelay
			: MaximumIdleDelay;

		return delay > TimeSpan.Zero
			? Task.Delay(delay, cancellationToken)
			: Task.CompletedTask;
	}

	private async Task PublishNodeUpdatesAsync(RuntimeGraphBuildResult graphBuildResult, CancellationToken cancellationToken)
	{
		foreach (var pair in graphBuildResult.NodeIdsByRuntime)
			await PublishNodeUpdateAsync(pair.Value, pair.Key, GetNodeStatus(pair.Key), GetNodeMessage(pair.Key), cancellationToken).ConfigureAwait(false);
	}

	private async Task PublishPreviewsAsync(RuntimeGraphBuildResult graphBuildResult, CancellationToken cancellationToken)
	{
		if (!_hasPreviewSubscribers())
			return;

		foreach (var pair in graphBuildResult.NodeIdsByRuntime)
		{
			if (!IsPreviewEnabled(pair.Value))
				continue;

			if (_request.Mode == ExecutionModeDto.Continuous)
			{
				QueuePreviewIfAvailable(pair.Value, pair.Key);
				continue;
			}

			var preview = CreatePreviewMessage(pair.Value, pair.Key);
			if (preview is not null)
				await PublishAsync(preview, cancellationToken).ConfigureAwait(false);
		}
	}

	private void QueuePreviewIfAvailable(string nodeId, Node node)
	{
		// Do not generate another image while this node already has a frame queued or being sent.
		// Apart from reducing conversion work, this keeps the two-buffer cache safe: a producer can
		// never wrap around and overwrite bytes still owned by an asynchronous WebSocket send.
		if (!_previewsInFlight.TryAdd(nodeId, 0))
			return;

		try
		{
			var preview = CreatePreviewMessage(nodeId, node);
			if (preview is null)
			{
				_previewsInFlight.TryRemove(nodeId, out _);
				return;
			}

			StampMessage(preview);
			if (!_previewPublicationQueue.Writer.TryWrite(new QueuedPreview(nodeId, preview)))
				_previewsInFlight.TryRemove(nodeId, out _);
		}
		catch
		{
			_previewsInFlight.TryRemove(nodeId, out _);
			throw;
		}
	}

	private ExecutionMessageDto? CreatePreviewMessage(string nodeId, Node node)
	{
		var previewImageMaxDimension = Volatile.Read(ref _previewImageMaxDimension);
		return RuntimePreviewFactory.CreatePreviewMessage(
			nodeId,
			node,
			previewImageMaxDimension,
			_previewImageBufferCache);
	}

	private async Task PublishQueuedPreviewsAsync(CancellationToken cancellationToken)
	{
		await foreach (var queuedPreview in _previewPublicationQueue.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
		{
			try
			{
				await _publishAsync(queuedPreview.Message, cancellationToken).ConfigureAwait(false);
			}
			finally
			{
				_previewsInFlight.TryRemove(queuedPreview.NodeId, out _);
			}
		}
	}

	private Task PublishNodeUpdateAsync(
		string nodeId,
		Node node,
		NodeExecutionStatusDto status,
		string? message,
		CancellationToken cancellationToken)
	{
		return PublishAsync(
			new ExecutionMessageDto
			{
				MessageType = ExecutionMessageTypeDto.NodeUpdate,
				NodeUpdate = new NodeExecutionUpdateDto
				{
					NodeId = nodeId,
					Status = status,
					Message = message,
					ExecutionDurationMs = node.LastExecutionTime.TotalMilliseconds,
					TimestampUtc = DateTimeOffset.UtcNow
				}
			},
			cancellationToken);
	}

	private Task PublishStateAsync(
		ExecutionStatusDto status,
		string? message,
		long framesProcessed,
		double? fps,
		double? lastExecutionDurationMs,
		ExecutionMessageTypeDto messageType,
		CancellationToken cancellationToken)
	{
		return PublishAsync(
			new ExecutionMessageDto
			{
				MessageType = messageType,
				ExecutionState = new ExecutionStateDto
				{
					ClientId = _request.ClientId,
					ExecutionId = ExecutionId,
					Status = status,
					Message = message,
					FramesProcessed = framesProcessed,
					FramesPerSecond = fps,
					LastExecutionDurationMs = lastExecutionDurationMs,
					TimestampUtc = DateTimeOffset.UtcNow
				},
				Error = status == ExecutionStatusDto.Failed ? message : null
			},
			cancellationToken);
	}

	private Task PublishFailureAsync(Exception exception, RuntimeGraphBuildResult? graphBuildResult, long framesProcessed)
	{
		var message = FormatFailureMessage(exception, graphBuildResult);
		return PublishStateAsync(
			ExecutionStatusDto.Failed,
			message,
			framesProcessed,
			null,
			null,
			ExecutionMessageTypeDto.Failure,
			CancellationToken.None);
	}

	private static string FormatFailureMessage(Exception exception, RuntimeGraphBuildResult? graphBuildResult)
	{
		if (exception is not NodeExecutionException nodeExecutionException)
			return exception.Message;

		var nodeType = nodeExecutionException.Node.GetType().Name;
		string? nodeId = null;
		graphBuildResult?.NodeIdsByRuntime.TryGetValue(nodeExecutionException.Node, out nodeId);
		var reason = nodeExecutionException.InnerException?.Message;
		var nodeDescription = string.IsNullOrWhiteSpace(nodeId)
			? nodeType
			: $"{nodeType} '{nodeId}'";

		return string.IsNullOrWhiteSpace(reason)
			? $"{nodeDescription} failed."
			: $"{nodeDescription} failed: {reason}";
	}

	private void ConfigureManualTriggerNodes(RuntimeGraphBuildResult graphBuildResult)
	{
		foreach (var pair in graphBuildResult.NodeIdsByRuntime)
		{
			if (pair.Key is not ManualTriggerNode manualTriggerNode)
				continue;

			manualTriggerNode.TriggerId = pair.Value;
			manualTriggerNode.TryConsumeExternalTrigger = TryConsumeManualTrigger;
		}
	}

	private bool TryConsumeManualTrigger(string nodeId)
	{
		if (string.IsNullOrWhiteSpace(nodeId))
			return false;

		lock (_manualTriggerSync)
		{
			if (!_manualTriggerCounts.TryGetValue(nodeId, out var count) || count <= 0)
				return false;

			if (count == 1)
				_manualTriggerCounts.Remove(nodeId);
			else
				_manualTriggerCounts[nodeId] = count - 1;

			return true;
		}
	}

	private bool TryUpdatePreviewEnabled(string nodeId, IEnumerable<NodePropertyDto> properties)
	{
		foreach (var property in properties)
		{
			if (!string.Equals(property.Name, NodePreviewSettings.ShowPreviewPropertyName, StringComparison.OrdinalIgnoreCase))
				continue;

			if (!bool.TryParse(property.Value, out var enabled))
				return false;

			lock (_previewSync)
			{
				if (enabled)
					_previewEnabledNodeIds.Add(nodeId);
				else
					_previewEnabledNodeIds.Remove(nodeId);
			}

			return true;
		}

		return false;
	}

	private bool IsPreviewEnabled(string nodeId)
	{
		lock (_previewSync)
			return _previewEnabledNodeIds.Contains(nodeId);
	}

	private static IEnumerable<NodePropertyDto> GetLiveProperties(Node node, IEnumerable<NodePropertyDto> properties)
	{
		foreach (var property in properties)
		{
			if (string.Equals(property.Name, NodePreviewSettings.ShowPreviewPropertyName, StringComparison.OrdinalIgnoreCase))
				continue;

			if (node is PolimagoClassifyNode &&
				string.Equals(property.Name, nameof(PolimagoClassifyNode.MinQuality), StringComparison.OrdinalIgnoreCase))
			{
				yield return property;
				continue;
			}

			if (node is MinosSearchNode &&
				!string.Equals(property.Name, nameof(MinosSearchNode.ClassifierPath), StringComparison.OrdinalIgnoreCase))
			{
				yield return property;
				continue;
			}

			if (node is ImageNode &&
				(string.Equals(property.Name, nameof(ImageNode.SelectedImageIndex), StringComparison.OrdinalIgnoreCase) ||
				 string.Equals(property.Name, nameof(ImageNode.IsPlaying), StringComparison.OrdinalIgnoreCase)))
			{
				yield return property;
				continue;
			}

			if (node is IInitializable)
				continue;

			yield return property;
		}
	}

	private async Task PublishAsync(ExecutionMessageDto message, CancellationToken cancellationToken)
	{
		StampMessage(message);
		await _publishAsync(message, cancellationToken).ConfigureAwait(false);
	}

	private void StampMessage(ExecutionMessageDto message)
	{
		message.ExecutionId = ExecutionId;
		message.TimestampUtc = DateTimeOffset.UtcNow;

		if (BinaryExecutionMessageCodec.TryGetImagePreview(message, out var imagePreview))
			imagePreview.PreviewSequence = Interlocked.Increment(ref _previewSequence);
	}

	private sealed record QueuedPreview(string NodeId, ExecutionMessageDto Message);

	private static string? GetNodeMessage(Node node)
	{
		return node switch
		{
			HistogramNode histogramNode => $"Mean {histogramNode.Mean:F2}, StdDev {histogramNode.StdDev:F2}",
			BlobNode blobNode => $"{blobNode.BlobCount} blob(s)",
			MinosSearchNode minosNode => $"{minosNode.ResultCount} match(es)",
			PolimagoClassifyNode classifyNode => $"{classifyNode.ResultCount} result(s)",
			GevServerNode gevServerNode => gevServerNode.LastStatus,
			ImageNode imageNode when imageNode.IsFolderSource => $"Image {imageNode.SelectedImageIndex + 1}/{imageNode.ImageCount} {(imageNode.IsPlaying ? "Playing" : "Stopped")}",
			CSharpNode csharpNode when !string.IsNullOrWhiteSpace(csharpNode.LastCompilationError) => csharpNode.LastCompilationError,
			_ => null
		};
	}

	private static NodeExecutionStatusDto GetNodeStatus(Node node)
	{
		if (node is CSharpNode csharpNode && !string.IsNullOrWhiteSpace(csharpNode.LastCompilationError))
			return NodeExecutionStatusDto.Failed;

		return NodeExecutionStatusDto.Succeeded;
	}

	private static double GetPreviewIntervalMilliseconds(int previewRefreshRate)
	{
		if (previewRefreshRate >= 1001)
			return 0;

		var rate = Math.Max(previewRefreshRate, 1);
		return 1000.0 / rate;
	}
}
