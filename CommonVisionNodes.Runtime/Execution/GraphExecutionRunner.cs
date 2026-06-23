using System.Diagnostics;
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
/// <param name="previewFactory">Factory used to build preview messages.</param>
/// <param name="publishAsync">Callback used to publish execution messages.</param>
/// <param name="onCompleted">Callback invoked after the runner exits.</param>
public sealed class GraphExecutionRunner(
	ExecutionRequestDto request,
	RuntimeGraphFactory graphFactory,
	RuntimePreviewFactory previewFactory,
	Func<ExecutionMessageDto, CancellationToken, Task> publishAsync,
	Action<GraphExecutionRunner> onCompleted) : IAsyncDisposable
{
    private readonly ExecutionRequestDto _request = request;
    private readonly RuntimeGraphFactory _graphFactory = graphFactory;
    private readonly RuntimePreviewFactory _previewFactory = previewFactory;
    private readonly Func<ExecutionMessageDto, CancellationToken, Task> _publishAsync = publishAsync;
    private readonly Action<GraphExecutionRunner> _onCompleted = onCompleted;
    private readonly HashSet<string> _previewEnabledNodeIds = request.Graph.Nodes
			.Where(node => !string.IsNullOrWhiteSpace(node.Id) && NodePreviewSettings.IsEnabled(node.Type, node.Properties))
			.Select(node => node.Id)
			.ToHashSet(StringComparer.OrdinalIgnoreCase);
    private readonly Lock _previewSync = new();
    private readonly Lock _manualTriggerSync = new();
    private readonly Dictionary<string, int> _manualTriggerCounts = new(StringComparer.OrdinalIgnoreCase);
    private readonly Lock _graphSync = new();
    private readonly CancellationTokenSource _cts = new();
    private int _previewRefreshRate = request.PreviewRefreshRate;
    private int _previewImageMaxDimension = request.PreviewImageMaxDimension;
    private Task? _executionTask;
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
                var elapsed = await ExecuteFrameAsync(graphBuildResult, cancellationToken).ConfigureAwait(false);
                framesProcessed = 1;
                await PublishPreviewsAsync(graphBuildResult, cancellationToken).ConfigureAwait(false);
                await PublishStateAsync(ExecutionStatusDto.Completed, "Execution completed.", framesProcessed, null, elapsed.TotalMilliseconds, ExecutionMessageTypeDto.Completed, cancellationToken).ConfigureAwait(false);
                return;
            }

            while (!cancellationToken.IsCancellationRequested)
            {
                var elapsed = await ExecuteFrameAsync(graphBuildResult, cancellationToken).ConfigureAwait(false);
                framesProcessed++;
                framesInWindow++;

                double? fps = null;
                if (fpsTimer.ElapsedMilliseconds >= 1000)
                {
                    fps = framesInWindow * 1000.0 / fpsTimer.ElapsedMilliseconds;
                    framesInWindow = 0;
                    fpsTimer.Restart();
                }

                var previewIntervalMs = GetPreviewIntervalMilliseconds(Volatile.Read(ref _previewRefreshRate));
                if (previewIntervalMs == 0 || previewTimer.Elapsed.TotalMilliseconds >= previewIntervalMs)
                {
                    // Preview generation can be expensive, especially with PNG fallbacks, so it is
                    // throttled independently from the graph execution loop.
                    await PublishPreviewsAsync(graphBuildResult, cancellationToken).ConfigureAwait(false);
                    previewTimer.Restart();
                }

                await PublishStateAsync(ExecutionStatusDto.Running, "Executing.", framesProcessed, fps, elapsed.TotalMilliseconds, ExecutionMessageTypeDto.ExecutionState, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (_cts.IsCancellationRequested)
        {
            await PublishStateAsync(ExecutionStatusDto.Stopped, "Execution stopped.", framesProcessed, null, null, ExecutionMessageTypeDto.ExecutionState, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await PublishFailureAsync(ex, framesProcessed).ConfigureAwait(false);
        }
        finally
        {
            lock (_graphSync)
            {
                if (ReferenceEquals(_activeGraphBuildResult, graphBuildResult))
                    _activeGraphBuildResult = null;
            }

            graphBuildResult?.Dispose();
            _onCompleted(this);
        }
    }

    private async Task<TimeSpan> ExecuteFrameAsync(RuntimeGraphBuildResult graphBuildResult, CancellationToken cancellationToken)
    {
        var executionTimer = Stopwatch.StartNew();

        try
        {
            lock (_graphSync)
                graphBuildResult.Graph.Execute();
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

            throw nodeExecutionException.InnerException ?? nodeExecutionException;
        }

        executionTimer.Stop();

        foreach (var pair in graphBuildResult.NodeIdsByRuntime)
            await PublishNodeUpdateAsync(pair.Value, pair.Key, GetNodeStatus(pair.Key), GetNodeMessage(pair.Key), cancellationToken).ConfigureAwait(false);

        return executionTimer.Elapsed;
    }

    private async Task PublishPreviewsAsync(RuntimeGraphBuildResult graphBuildResult, CancellationToken cancellationToken)
    {
        foreach (var pair in graphBuildResult.NodeIdsByRuntime)
        {
            if (!IsPreviewEnabled(pair.Value))
                continue;

            var previewImageMaxDimension = Volatile.Read(ref _previewImageMaxDimension);
            var preview = RuntimePreviewFactory.CreatePreviewMessage(pair.Value, pair.Key, previewImageMaxDimension);
            if (preview is not null)
                await PublishAsync(preview, cancellationToken).ConfigureAwait(false);
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

    private Task PublishFailureAsync(Exception exception, long framesProcessed)
    {
        var message = exception.Message;
        return PublishStateAsync(
            ExecutionStatusDto.Failed,
            message,
            framesProcessed,
            null,
            null,
            ExecutionMessageTypeDto.Failure,
            CancellationToken.None);
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
        message.ExecutionId = ExecutionId;
        message.TimestampUtc = DateTimeOffset.UtcNow;
        await _publishAsync(message, cancellationToken).ConfigureAwait(false);
    }

    private static string? GetNodeMessage(Node node)
    {
        return node switch
        {
            HistogramNode histogramNode => $"Mean {histogramNode.Mean:F2}, StdDev {histogramNode.StdDev:F2}",
            BlobNode blobNode => $"{blobNode.BlobCount} blob(s)",
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
