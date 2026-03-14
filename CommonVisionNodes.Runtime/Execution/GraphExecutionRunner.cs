using System.Diagnostics;
using CommonVisionNodes.Contracts;

namespace CommonVisionNodes.Runtime.Execution;

public sealed class GraphExecutionRunner : IAsyncDisposable
{
    private readonly ExecutionRequestDto _request;
    private readonly RuntimeGraphFactory _graphFactory;
    private readonly RuntimePreviewFactory _previewFactory;
    private readonly Func<ExecutionMessageDto, CancellationToken, Task> _publishAsync;
    private readonly Action<GraphExecutionRunner> _onCompleted;
    private readonly CancellationTokenSource _cts = new();
    private Task? _executionTask;

    public GraphExecutionRunner(
        ExecutionRequestDto request,
        RuntimeGraphFactory graphFactory,
        RuntimePreviewFactory previewFactory,
        Func<ExecutionMessageDto, CancellationToken, Task> publishAsync,
        Action<GraphExecutionRunner> onCompleted)
    {
        _request = request;
        _graphFactory = graphFactory;
        _previewFactory = previewFactory;
        _publishAsync = publishAsync;
        _onCompleted = onCompleted;
        ExecutionId = Guid.NewGuid().ToString("N");
    }

    public string ExecutionId { get; }

    public void Start()
    {
        if (_executionTask is not null)
            return;

        _executionTask = Task.Run(() => RunAsync(_cts.Token));
    }

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

    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
        _cts.Dispose();
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        RuntimeGraphBuildResult? graphBuildResult = null;
        var framesProcessed = 0L;
        var previewIntervalMs = GetPreviewIntervalMilliseconds(_request.PreviewRefreshRate);
        var previewTimer = Stopwatch.StartNew();
        var fpsTimer = Stopwatch.StartNew();
        var framesInWindow = 0;

        try
        {
            await PublishStateAsync(ExecutionStatusDto.Starting, "Building execution graph.", framesProcessed, null, null, ExecutionMessageTypeDto.ExecutionState, cancellationToken).ConfigureAwait(false);

            graphBuildResult = _graphFactory.Build(_request.Graph);

            await PublishStateAsync(ExecutionStatusDto.Initializing, "Initializing runtime nodes.", framesProcessed, null, null, ExecutionMessageTypeDto.ExecutionState, cancellationToken).ConfigureAwait(false);
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

                if (previewIntervalMs == 0 || previewTimer.ElapsedMilliseconds >= previewIntervalMs)
                {
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
            graphBuildResult?.Dispose();
            _onCompleted(this);
        }
    }

    private async Task<TimeSpan> ExecuteFrameAsync(RuntimeGraphBuildResult graphBuildResult, CancellationToken cancellationToken)
    {
        var executionTimer = Stopwatch.StartNew();

        try
        {
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
            var preview = _previewFactory.CreatePreviewMessage(pair.Value, pair.Key);
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

    private async Task PublishAsync(ExecutionMessageDto message, CancellationToken cancellationToken)
    {
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

    private static int GetPreviewIntervalMilliseconds(int previewRefreshRate)
    {
        if (previewRefreshRate >= 1001)
            return 0;

        var rate = Math.Max(previewRefreshRate, 1);
        return (int)Math.Ceiling(1000.0 / rate);
    }
}
