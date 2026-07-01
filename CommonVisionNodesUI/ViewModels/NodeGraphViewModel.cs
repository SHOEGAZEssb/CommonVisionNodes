using System.ComponentModel;
using System.Globalization;
using CommonVisionNodes.Contracts;
using CommonVisionNodesUI.Services;
using Microsoft.UI.Dispatching;
using Windows.Storage;

namespace CommonVisionNodesUI.ViewModels;

/// <summary>
/// Option shown by the UI for the maximum preview image dimension.
/// </summary>
/// <param name="Value">Maximum preview long edge in pixels, or 0 to disable downscaling.</param>
/// <param name="Label">User-facing option label.</param>
public sealed record PreviewImageMaxDimensionOption(int Value, string Label);

/// <summary>
/// View model that owns graph editing, execution control, and backend message handling.
/// </summary>
/// <remarks>
/// Creates the graph view model.
/// </remarks>
/// <param name="backendClient">Backend API client.</param>
public partial class NodeGraphViewModel(IBackendClient backendClient) : ObservableObject
{
    private const int DefaultPreviewRefreshRate = 30;
    private const int DefaultPreviewImageMaxDimension = 1280;
    private const string PreviewRefreshRateSettingKey = "PreviewRefreshRate";
    private const string PreviewImageMaxDimensionSettingKey = "PreviewImageMaxDimension";

    private readonly IBackendClient _backendClient = backendClient;
    private readonly DispatcherQueue _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
    private readonly Dictionary<string, NodeDefinitionDto> _nodeDefinitions = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, NodeViewModel> _nodesById = new(StringComparer.OrdinalIgnoreCase);
    private readonly string _clientId = Guid.NewGuid().ToString("N");
    private CancellationTokenSource? _listenerCts;
    private Task? _listenerTask;
    private readonly SemaphoreSlim _executionRestartGate = new(1, 1);
    private readonly SemaphoreSlim _nodePropertiesUpdateGate = new(1, 1);
    private readonly object _nodePropertiesUpdateSync = new();
    private readonly Dictionary<string, CancellationTokenSource> _nodePropertiesDebounceCtsByNodeId = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, long> _nodePropertiesUpdateVersionsByNodeId = new(StringComparer.OrdinalIgnoreCase);
    private double _nextNodeX = 50;
    private double _nextNodeY = 50;
    private bool _initialized;
    private Task? _initializeTask;
    private CancellationTokenSource? _graphRestartDebounceCts;
    private CancellationTokenSource? _previewSettingsDebounceCts;
    private long _executionRestartVersion;
    private string? _activeExecutionId;
    private readonly HashSet<string> _terminalExecutionIds = new(StringComparer.OrdinalIgnoreCase);

	/// <summary>
	/// Nodes currently present on the canvas.
	/// </summary>
	public ObservableCollection<NodeViewModel> Nodes { get; } = [];

    /// <summary>
    /// Connections currently present on the canvas.
    /// </summary>
    public ObservableCollection<ConnectionViewModel> Connections { get; } = [];

    /// <summary>
    /// Supported preview downscale options.
    /// </summary>
    public IReadOnlyList<PreviewImageMaxDimensionOption> PreviewImageMaxDimensionOptions { get; } =
    [
        new(0, "Off (full resolution)"),
        new(1600, "1600 px"),
        new(1280, "1280 px"),
        new(960, "960 px"),
        new(640, "640 px"),
        new(480, "480 px"),
        new(360, "360 px"),
        new(320, "320 px"),
        new(240, "240 px"),
        new(160, "160 px")
    ];

	[ObservableProperty]
	public partial NodeViewModel? SelectedNode { get; set; }

	[ObservableProperty]
	public partial bool IsRunning { get; set; }
	[ObservableProperty]
	public partial double Fps { get; set; }

	[ObservableProperty]
	public partial string LastExecutionTimeText { get; set; } = "-";

	[ObservableProperty]
	public partial string LastExecutionError { get; set; } = string.Empty;

	[ObservableProperty]
	public partial int PreviewRefreshRate { get; set; } = Math.Clamp(ReadIntSetting(PreviewRefreshRateSettingKey, DefaultPreviewRefreshRate), 1, 1001);

	[ObservableProperty]
	public partial int PreviewImageMaxDimension { get; set; } = Math.Max(0, ReadIntSetting(PreviewImageMaxDimensionSettingKey, DefaultPreviewImageMaxDimension));

	/// <summary>
	/// Text representation of the preview refresh rate.
	/// </summary>
	public string PreviewRefreshRateText => PreviewRefreshRate >= 1001 ? "inf" : PreviewRefreshRate.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// Text representation of the preview image downscale limit.
    /// </summary>
    public string PreviewImageMaxDimensionText => PreviewImageMaxDimension <= 0 ? "Off" : $"{PreviewImageMaxDimension}px";

    /// <summary>
    /// Initializes node definitions and starts the execution message listener once.
    /// </summary>
    /// <returns>A task that completes when initialization is done.</returns>
    public Task InitializeAsync()
    {
        if (_initialized)
            return Task.CompletedTask;

        return _initializeTask ??= InitializeCoreAsync();
    }

    private async Task InitializeCoreAsync()
    {
        try
        {
            await RefreshNodeDefinitionsAsync();

            _listenerCts = new CancellationTokenSource();
            _listenerTask = Task.Run(() => _backendClient.ListenAsync(_clientId, HandleExecutionMessageAsync, _listenerCts.Token));
            _initialized = true;
        }
        finally
        {
            _initializeTask = null;
        }
    }

    /// <summary>
    /// Reloads node definitions from the backend and refreshes existing node view models.
    /// </summary>
    /// <returns>A task that completes when definitions have been refreshed.</returns>
    public async Task RefreshNodeDefinitionsAsync()
    {
        var definitions = await _backendClient.GetNodeDefinitionsAsync();
        _nodeDefinitions.Clear();
        foreach (var definition in definitions)
            _nodeDefinitions[definition.Type] = definition;

        foreach (var node in Nodes)
        {
            if (_nodeDefinitions.TryGetValue(node.Node.Type, out var updatedDefinition))
                node.RefreshDefinition(updatedDefinition);
        }
    }

    /// <summary>
    /// Selects a node and updates selection state on the previous selection.
    /// </summary>
    /// <param name="node">Node to select, or <c>null</c> to clear selection.</param>
    public void SelectNode(NodeViewModel? node)
    {
        SelectedNode?.IsSelected = false;

        SelectedNode = node;
        if (node is not null)
        {
            node.IsSelected = true;
            node.IsGraphRunning = IsRunning;
        }
    }

    /// <summary>
    /// Serializes the current editor graph to a DTO.
    /// </summary>
    /// <returns>Graph DTO representing the current editor state.</returns>
    public GraphDto ToGraphDto()
        => new()
        {
            Nodes = [.. Nodes.Select(node => node.ToNodeDtoClone())],
            Connections = [.. Connections.Select(connection => new ConnectionDto
            {
                OutputNodeId = connection.Connection.OutputNodeId,
                OutputPortName = connection.Connection.OutputPortName,
                InputNodeId = connection.Connection.InputNodeId,
                InputPortName = connection.Connection.InputPortName
            })]
        };

    /// <summary>
    /// Replaces the current editor graph from a serialized DTO.
    /// </summary>
    /// <param name="graph">Graph DTO to load.</param>
    /// <returns>A task that completes when the graph is loaded.</returns>
    public async Task LoadGraphAsync(GraphDto graph)
    {
        await InitializeAsync();
        ClearGraph();

        foreach (var node in graph.Nodes)
        {
            if (!_nodeDefinitions.TryGetValue(node.Type, out var definition))
                continue;

            var viewModel = NodeViewModelFactory.Create(node, definition, RefreshNodeDefinitionsAsync);
            AddLoadedNode(viewModel);
        }

        foreach (var connection in graph.Connections)
        {
            if (!_nodesById.TryGetValue(connection.OutputNodeId, out var outputNode) ||
                !_nodesById.TryGetValue(connection.InputNodeId, out var inputNode))
                continue;

            var outputPort = outputNode.OutputPorts.FirstOrDefault(port => port.Port.Name == connection.OutputPortName);
            var inputPort = inputNode.InputPorts.FirstOrDefault(port => port.Port.Name == connection.InputPortName);
            if (outputPort is not null && inputPort is not null)
                TryConnect(outputPort, inputPort);
        }
    }

    /// <summary>
    /// Attempts to connect two ports, replacing an existing input connection when necessary.
    /// </summary>
    /// <param name="portA">First port involved in the connection gesture.</param>
    /// <param name="portB">Second port involved in the connection gesture.</param>
    /// <returns><c>true</c> when a connection was created.</returns>
    public bool TryConnect(PortViewModel portA, PortViewModel portB)
    {
        var outputPort = portA.Port.Direction == PortDirectionDto.Output ? portA : portB;
        var inputPort = portA.Port.Direction == PortDirectionDto.Input ? portA : portB;

        if (outputPort.Port.Direction != PortDirectionDto.Output ||
            inputPort.Port.Direction != PortDirectionDto.Input)
            return false;

        if (ReferenceEquals(outputPort.ParentNode, inputPort.ParentNode))
            return false;

        if (!AreTypesCompatible(outputPort.Port.Type, inputPort.Port.Type))
            return false;

        if (Connections.Any(connection => connection.Target == inputPort))
            DisconnectPort(inputPort);

        if (Connections.Any(connection => connection.Source == outputPort && connection.Target == inputPort))
            return false;

        Connections.Add(new ConnectionViewModel(
            new ConnectionDto
            {
                OutputNodeId = outputPort.ParentNode.Node.Id,
                OutputPortName = outputPort.Port.Name,
                InputNodeId = inputPort.ParentNode.Node.Id,
                InputPortName = inputPort.Port.Name
            },
            outputPort,
            inputPort));

        return true;
    }

    /// <summary>
    /// Removes every connection attached to a port.
    /// </summary>
    /// <param name="port">Port to disconnect.</param>
    public void DisconnectPort(PortViewModel port)
    {
        var toRemove = Connections
            .Where(connection => connection.Source == port || connection.Target == port)
            .ToList();

        foreach (var connection in toRemove)
            Connections.Remove(connection);
    }

    [RelayCommand]
    private async Task AddImageNode()
    {
        await InitializeAsync();
        AddNode("ImageNode");
    }

    [RelayCommand]
    private async Task AddSaveImageNode()
    {
        await InitializeAsync();
        AddNode("SaveImageNode");
    }

    [RelayCommand]
    private async Task AddGevServerNode()
    {
        await InitializeAsync();
        AddNode("GevServerNode");
    }

    [RelayCommand]
    private async Task AddDeviceNode()
    {
        await InitializeAsync();
        AddNode("DeviceNode");
    }

    [RelayCommand]
    private async Task AddBinarizeNode()
    {
        await InitializeAsync();
        AddNode("BinarizeNode");
    }

    [RelayCommand]
    private async Task AddSubImageNode()
    {
        await InitializeAsync();
        AddNode("SubImageNode");
    }

    [RelayCommand]
    private async Task AddMatrixTransformNode()
    {
        await InitializeAsync();
        AddNode("MatrixTransformNode");
    }

    [RelayCommand]
    private async Task AddImageGeneratorNode()
    {
        await InitializeAsync();
        AddNode("ImageGeneratorNode");
    }

    [RelayCommand]
    private async Task AddTimeTriggerNode()
    {
        await InitializeAsync();
        AddNode("TimeTriggerNode");
    }

    [RelayCommand]
    private async Task AddManualTriggerNode()
    {
        await InitializeAsync();
        AddNode("ManualTriggerNode");
    }

    [RelayCommand]
    private async Task AddFilterNode()
    {
        await InitializeAsync();
        AddNode("FilterNode");
    }

    [RelayCommand]
    private async Task AddHistogramNode()
    {
        await InitializeAsync();
        AddNode("HistogramNode");
    }

    [RelayCommand]
    private async Task AddMorphologyNode()
    {
        await InitializeAsync();
        AddNode("MorphologyNode");
    }

    [RelayCommand]
    private async Task AddBlobNode()
    {
        await InitializeAsync();
        AddNode("BlobNode");
    }

    [RelayCommand]
    private async Task AddNormalizeNode()
    {
        await InitializeAsync();
        AddNode("NormalizeNode");
    }

    [RelayCommand]
    private async Task AddPolimagoClassifyNode()
    {
        await InitializeAsync();
        AddNode("PolimagoClassifyNode");
    }

    [RelayCommand]
    private async Task AddGenericVisualizerNode()
    {
        await InitializeAsync();
        AddNode("GenericVisualizerNode");
    }

    [RelayCommand]
    private async Task AddCSharpNode()
    {
        await InitializeAsync();
        AddNode("CSharpNode");
    }

    [RelayCommand]
    private void RemoveNode(NodeViewModel nodeViewModel)
    {
        nodeViewModel.ConfigurationChanged -= OnNodeConfigurationChanged;
        nodeViewModel.PropertyChanged -= OnNodePropertyChanged;
        if (nodeViewModel is ManualTriggerNodeViewModel manualTriggerNode)
            manualTriggerNode.TriggerRequested -= OnManualTriggerRequested;

        var connectionsToRemove = Connections
            .Where(connection => connection.Source.ParentNode == nodeViewModel || connection.Target.ParentNode == nodeViewModel)
            .ToList();

        foreach (var connection in connectionsToRemove)
            Connections.Remove(connection);

        Nodes.Remove(nodeViewModel);
        _nodesById.Remove(nodeViewModel.Node.Id);

        if (SelectedNode == nodeViewModel)
            SelectNode(null);
    }

    [RelayCommand]
    private void RemoveSelectedNode()
    {
        if (SelectedNode is not null)
            RemoveNode(SelectedNode);
    }

    [RelayCommand]
    private async Task ExecuteGraphAsync()
    {
        await InitializeAsync();
        var accepted = await _backendClient.ExecuteAsync(CreateExecutionRequest(ExecutionModeDto.Single));
        ApplyExecutionAccepted(accepted);
    }

    [RelayCommand]
    private async Task ToggleRunAsync()
    {
        await InitializeAsync();

        if (IsRunning)
        {
            InvalidatePendingExecutionRestarts();
            CancelPendingExecutionRestart();
            CancelPendingPreviewSettingsUpdate();
            CancelPendingNodePropertiesUpdate();
            await _executionRestartGate.WaitAsync();
            try
            {
                await _backendClient.StopAsync(_clientId);
            }
            finally
            {
                _executionRestartGate.Release();
            }

            IsRunning = false;
            return;
        }

        InvalidatePendingExecutionRestarts();
        CancelPendingExecutionRestart();
        CancelPendingNodePropertiesUpdate();
        var accepted = await _backendClient.ExecuteAsync(CreateExecutionRequest(ExecutionModeDto.Continuous));
        ApplyExecutionAccepted(accepted);
    }

    /// <summary>
    /// Clears all graph nodes, connections, and selection state.
    /// </summary>
    public void ClearGraph()
    {
        SelectNode(null);
        Connections.Clear();
        foreach (var node in Nodes)
        {
            node.ConfigurationChanged -= OnNodeConfigurationChanged;
            node.PropertyChanged -= OnNodePropertyChanged;
            if (node is ManualTriggerNodeViewModel manualTriggerNode)
                manualTriggerNode.TriggerRequested -= OnManualTriggerRequested;
        }
        Nodes.Clear();
        _nodesById.Clear();
        _nextNodeX = 50;
        _nextNodeY = 50;
    }

    /// <summary>
    /// Requests standalone generated code for the current graph.
    /// </summary>
    /// <returns>Generated C# source code.</returns>
    public Task<string> GenerateCodeAsync() => _backendClient.GenerateCodeAsync(ToGraphDto());

    /// <summary>
    /// Stops background listeners and releases debounce timers.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        InvalidatePendingExecutionRestarts();
        CancelPendingExecutionRestart();
        CancelPendingPreviewSettingsUpdate();
        CancelPendingNodePropertiesUpdate();

        if (_listenerCts is not null)
        {
            _listenerCts.Cancel();
            if (_listenerTask is not null)
            {
                try
                {
                    await _listenerTask;
                }
                catch
                {
                    // Ignore listener shutdown failures.
                }
            }
            _listenerCts.Dispose();
            _listenerCts = null;
        }
    }

    partial void OnPreviewRefreshRateChanged(int value)
    {
        if (value is < 1 or > 1001)
        {
            PreviewRefreshRate = Math.Clamp(value, 1, 1001);
            return;
        }

        OnPropertyChanged(nameof(PreviewRefreshRateText));
        WriteIntSetting(PreviewRefreshRateSettingKey, value);
        ScheduleRunningPreviewSettingsUpdate();
    }

    partial void OnPreviewImageMaxDimensionChanged(int value)
    {
        if (value < 0)
        {
            PreviewImageMaxDimension = 0;
            return;
        }

        OnPropertyChanged(nameof(PreviewImageMaxDimensionText));
        WriteIntSetting(PreviewImageMaxDimensionSettingKey, value);
        ScheduleRunningPreviewSettingsUpdate();
    }

    partial void OnIsRunningChanged(bool value)
    {
        foreach (var node in Nodes)
            node.IsGraphRunning = value;
    }

    private void AddNode(string type)
    {
        if (!_nodeDefinitions.TryGetValue(type, out var definition))
            return;

        var node = new NodeDto
        {
            Type = type,
            X = _nextNodeX,
            Y = _nextNodeY,
            Properties = [.. definition.Properties.Select(property => new NodePropertyDto
            {
                Name = property.Name,
                Value = property.DefaultValue
            })]
		};

        var viewModel = NodeViewModelFactory.Create(node, definition, RefreshNodeDefinitionsAsync);
        AddLoadedNode(viewModel);

        _nextNodeX += 60;
        if (_nextNodeX > 500)
        {
            _nextNodeX = 50;
            _nextNodeY += 120;
        }
    }

    private void AddLoadedNode(NodeViewModel viewModel)
    {
        viewModel.ConfigurationChanged += OnNodeConfigurationChanged;
        viewModel.PropertyChanged += OnNodePropertyChanged;
        viewModel.IsGraphRunning = IsRunning;
        if (viewModel is ManualTriggerNodeViewModel manualTriggerNode)
            manualTriggerNode.TriggerRequested += OnManualTriggerRequested;

        Nodes.Add(viewModel);
        _nodesById[viewModel.Node.Id] = viewModel;
    }

    private async void OnManualTriggerRequested(object? sender, EventArgs e)
    {
        if (sender is not ManualTriggerNodeViewModel manualTriggerNode)
            return;

        if (!IsRunning)
        {
            manualTriggerNode.MarkTriggerUnavailable();
            return;
        }

        try
        {
            await _backendClient.TriggerNodeAsync(
                new TriggerNodeRequestDto
                {
                    ClientId = _clientId,
                    NodeId = manualTriggerNode.Node.Id
                });
            manualTriggerNode.MarkTriggerQueued();
        }
        catch
        {
            manualTriggerNode.MarkTriggerFailed();
        }
    }

    private async void OnNodeConfigurationChanged(object? sender, EventArgs e)
    {
        if (sender is not NodeViewModel node || !IsRunning || !node.IsEditableWhileRunning)
            return;

        await UpdateRunningNodePropertiesAsync(node);
    }

    private async void OnNodePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(NodeViewModel.ShowPreview) || !IsRunning || sender is not NodeViewModel node)
            return;

        await UpdateRunningNodePropertiesAsync(node);
    }

    private async Task HandleExecutionMessageAsync(ExecutionMessageDto message)
    {
        await EnqueueAsync(() => ApplyExecutionMessage(message));
    }

    private void ApplyExecutionMessage(ExecutionMessageDto message)
    {
        TrackStartingExecution(message);

        if (IsStaleExecutionMessage(message))
            return;

        switch (message.MessageType)
        {
            case ExecutionMessageTypeDto.ExecutionState:
            case ExecutionMessageTypeDto.Completed:
            case ExecutionMessageTypeDto.Failure:
                ApplyExecutionState(message.ExecutionState);
                if (message.MessageType == ExecutionMessageTypeDto.Failure &&
                    message.ExecutionState is null &&
                    !string.IsNullOrWhiteSpace(message.Error))
                {
                    LastExecutionError = message.Error;
                }
                break;
            case ExecutionMessageTypeDto.NodeUpdate:
                if (message.NodeUpdate is not null && _nodesById.TryGetValue(message.NodeUpdate.NodeId, out var node))
                    node.ApplyExecutionUpdate(message.NodeUpdate);
                break;
            case ExecutionMessageTypeDto.ImagePreview:
                if (message.ImagePreview is not null && _nodesById.TryGetValue(message.ImagePreview.NodeId, out var imageNode))
                    imageNode.ApplyImagePreview(message.ImagePreview);
                break;
            case ExecutionMessageTypeDto.HistogramPreview:
                if (message.HistogramPreview is not null && _nodesById.TryGetValue(message.HistogramPreview.NodeId, out var histogramNode))
                    histogramNode.ApplyHistogramPreview(message.HistogramPreview);
                break;
            case ExecutionMessageTypeDto.BlobPreview:
                if (message.BlobPreview is not null && _nodesById.TryGetValue(message.BlobPreview.NodeId, out var blobNode))
                    blobNode.ApplyBlobPreview(message.BlobPreview);
                break;
            case ExecutionMessageTypeDto.ClassificationPreview:
                if (message.ClassificationPreview is not null && _nodesById.TryGetValue(message.ClassificationPreview.NodeId, out var classifyNode))
                    classifyNode.ApplyClassificationPreview(message.ClassificationPreview);
                break;
            case ExecutionMessageTypeDto.TextPreview:
                if (message.TextPreview is not null && _nodesById.TryGetValue(message.TextPreview.NodeId, out var textNode))
                    textNode.ApplyTextPreview(message.TextPreview);
                break;
        }
    }

    private void ApplyExecutionState(ExecutionStateDto? state)
    {
        if (state is null)
            return;

        IsRunning = state.Status is ExecutionStatusDto.Starting or ExecutionStatusDto.Initializing or ExecutionStatusDto.Running;
        Fps = IsRunning ? state.FramesPerSecond ?? Fps : 0;
        LastExecutionTimeText = state.LastExecutionDurationMs.HasValue
            ? FormatExecutionTime(state.LastExecutionDurationMs.Value)
            : "-";
        LastExecutionError = state.Status == ExecutionStatusDto.Failed
            ? state.Message ?? "Execution failed."
            : string.Empty;

        foreach (var node in Nodes)
            node.ApplyExecutionState(state);

        if (state.Status is ExecutionStatusDto.Completed or ExecutionStatusDto.Stopped or ExecutionStatusDto.Failed &&
            IsCurrentExecutionId(state.ExecutionId))
        {
            _activeExecutionId = null;
        }

        if (state.Status is ExecutionStatusDto.Completed or ExecutionStatusDto.Stopped or ExecutionStatusDto.Failed &&
            !string.IsNullOrWhiteSpace(state.ExecutionId))
        {
            _terminalExecutionIds.Add(state.ExecutionId);
        }
    }

    private Task EnqueueAsync(Action action)
    {
        if (_dispatcherQueue.HasThreadAccess)
        {
            action();
            return Task.CompletedTask;
        }

        var completionSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_dispatcherQueue.TryEnqueue(() =>
            {
                try
                {
                    action();
                    completionSource.SetResult();
                }
                catch (Exception ex)
                {
                    completionSource.SetException(ex);
                }
            }))
        {
            completionSource.SetException(new InvalidOperationException("Unable to dispatch work to the UI thread."));
        }

        return completionSource.Task;
    }

    private static bool AreTypesCompatible(string outputType, string inputType)
        => string.Equals(outputType, inputType, StringComparison.OrdinalIgnoreCase)
            || string.Equals(inputType, "Any", StringComparison.OrdinalIgnoreCase)
            || string.Equals(outputType, "Any", StringComparison.OrdinalIgnoreCase);

    private static string FormatExecutionTime(double executionDurationMs)
        => executionDurationMs >= 1.0
            ? $"{executionDurationMs:F1} ms"
            : $"{executionDurationMs * 1000:F0} us";

    private ExecutionRequestDto CreateExecutionRequest(ExecutionModeDto mode)
        => new()
        {
            ClientId = _clientId,
            Graph = ToGraphDto(),
            Mode = mode,
            PreviewRefreshRate = PreviewRefreshRate,
            PreviewImageMaxDimension = Math.Max(0, PreviewImageMaxDimension)
        };

    private void ScheduleRunningExecutionRestart()
    {
        if (IsRunning)
            _ = RestartContinuousExecutionAsync();
    }

    private void CancelPendingExecutionRestart()
    {
        _graphRestartDebounceCts?.Cancel();
        _graphRestartDebounceCts?.Dispose();
        _graphRestartDebounceCts = null;
    }

    private void ScheduleRunningPreviewSettingsUpdate()
    {
        if (IsRunning)
            _ = UpdateRunningPreviewSettingsAsync();
    }

    private void CancelPendingPreviewSettingsUpdate()
    {
        _previewSettingsDebounceCts?.Cancel();
        _previewSettingsDebounceCts?.Dispose();
        _previewSettingsDebounceCts = null;
    }

    private void CancelPendingNodePropertiesUpdate()
    {
        lock (_nodePropertiesUpdateSync)
        {
            foreach (var cts in _nodePropertiesDebounceCtsByNodeId.Values)
                cts.Cancel();

            _nodePropertiesDebounceCtsByNodeId.Clear();
        }
    }

    private async Task UpdateRunningPreviewSettingsAsync()
    {
        CancelPendingPreviewSettingsUpdate();

        var cts = new CancellationTokenSource();
        _previewSettingsDebounceCts = cts;

        try
        {
            await Task.Delay(200, cts.Token);

            if (cts.IsCancellationRequested || !IsRunning)
                return;

            // Preview settings are debounced because slider changes can arrive much faster than
            // the backend needs to reconfigure a running graph.
            await _backendClient.UpdateExecutionSettingsAsync(
                new UpdateExecutionSettingsRequestDto
                {
                    ClientId = _clientId,
                    ExecutionId = _activeExecutionId ?? string.Empty,
                    PreviewRefreshRate = PreviewRefreshRate,
                    PreviewImageMaxDimension = Math.Max(0, PreviewImageMaxDimension)
                },
                CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
            // A newer preview setting superseded this update request.
        }
        catch
        {
            // The active run may have stopped before the debounced settings update reached the backend.
        }
        finally
        {
            if (ReferenceEquals(_previewSettingsDebounceCts, cts))
            {
                _previewSettingsDebounceCts.Dispose();
                _previewSettingsDebounceCts = null;
            }
            else
            {
                cts.Dispose();
            }
        }
    }

    private async Task RestartContinuousExecutionAsync()
    {
        var restartVersion = Interlocked.Increment(ref _executionRestartVersion);
        CancelPendingExecutionRestart();

        var cts = new CancellationTokenSource();
        _graphRestartDebounceCts = cts;

        try
        {
            await Task.Delay(200, cts.Token);

            if (cts.IsCancellationRequested || !IsRunning || restartVersion != Interlocked.Read(ref _executionRestartVersion))
                return;

            // Most node property edits require rebuilding runtime resources, so the running graph
            // is replaced after the user pauses editing briefly.
            await _executionRestartGate.WaitAsync();
            try
            {
                if (!IsRunning || restartVersion != Interlocked.Read(ref _executionRestartVersion))
                    return;

                var accepted = await _backendClient.ExecuteAsync(CreateExecutionRequest(ExecutionModeDto.Continuous));
                if (restartVersion == Interlocked.Read(ref _executionRestartVersion))
                    ApplyExecutionAccepted(accepted);
            }
            finally
            {
                _executionRestartGate.Release();
            }
        }
        catch (OperationCanceledException)
        {
            // A newer change superseded this restart request.
        }
        catch
        {
            // The active run may have stopped or the backend may still be replacing a previous run.
        }
        finally
        {
            if (ReferenceEquals(_graphRestartDebounceCts, cts))
            {
                _graphRestartDebounceCts.Dispose();
                _graphRestartDebounceCts = null;
            }
            else
            {
                cts.Dispose();
            }
        }
    }

    private void ApplyExecutionAccepted(ExecutionAcceptedDto accepted)
    {
        if (!string.IsNullOrWhiteSpace(accepted.ExecutionId))
        {
            if (_terminalExecutionIds.Contains(accepted.ExecutionId))
                return;

            _activeExecutionId = accepted.ExecutionId;
        }

        IsRunning = accepted.Status is ExecutionStatusDto.Starting or ExecutionStatusDto.Initializing or ExecutionStatusDto.Running;
        LastExecutionError = string.Empty;
    }

    private bool IsStaleExecutionMessage(ExecutionMessageDto message)
    {
        var messageExecutionId = GetMessageExecutionId(message);

        return !string.IsNullOrWhiteSpace(messageExecutionId) &&
            !string.IsNullOrWhiteSpace(_activeExecutionId) &&
            !string.Equals(messageExecutionId, _activeExecutionId, StringComparison.OrdinalIgnoreCase);
    }

    private void TrackStartingExecution(ExecutionMessageDto message)
    {
        if (message.ExecutionState?.Status != ExecutionStatusDto.Starting)
            return;

        var messageExecutionId = GetMessageExecutionId(message);
        if (!string.IsNullOrWhiteSpace(messageExecutionId))
        {
            if (_terminalExecutionIds.Contains(messageExecutionId))
                return;

            if (!string.IsNullOrWhiteSpace(_activeExecutionId) &&
                !string.Equals(messageExecutionId, _activeExecutionId, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _terminalExecutionIds.Remove(messageExecutionId);
            _activeExecutionId = messageExecutionId;
        }
    }

    private bool IsCurrentExecutionId(string? executionId)
        => !string.IsNullOrWhiteSpace(executionId) &&
            string.Equals(executionId, _activeExecutionId, StringComparison.OrdinalIgnoreCase);

    private static string? GetMessageExecutionId(ExecutionMessageDto message)
        => !string.IsNullOrWhiteSpace(message.ExecutionId)
            ? message.ExecutionId
            : message.ExecutionState?.ExecutionId;

    private void InvalidatePendingExecutionRestarts()
        => Interlocked.Increment(ref _executionRestartVersion);

    private async Task UpdateRunningNodePropertiesAsync(NodeViewModel node)
    {
        var nodeId = node.Node.Id;
        var version = ScheduleNodePropertiesUpdate(nodeId, out var cts);

        try
        {
            await Task.Delay(150, cts.Token);

            await _nodePropertiesUpdateGate.WaitAsync(CancellationToken.None);
            try
            {
                if (!IsRunning || !IsLatestNodePropertiesUpdate(nodeId, version))
                    return;

                var executionId = _activeExecutionId;
                if (string.IsNullOrWhiteSpace(executionId))
                    return;

                await _backendClient.UpdateNodePropertiesAsync(
                    new UpdateNodePropertiesRequestDto
                    {
                        ClientId = _clientId,
                        ExecutionId = executionId,
                        NodeId = nodeId,
                        Properties = [.. node.ToNodeDtoClone().Properties]
                    },
                    CancellationToken.None);
            }
            finally
            {
                _nodePropertiesUpdateGate.Release();
            }
        }
        catch (OperationCanceledException)
        {
            // A newer property edit superseded this debounced update.
        }
        catch
        {
            // Live parameter updates are best-effort; unsupported edits will take effect on the next run.
        }
        finally
        {
            CompleteNodePropertiesUpdate(nodeId, version, cts);
        }
    }

    private long ScheduleNodePropertiesUpdate(string nodeId, out CancellationTokenSource cts)
    {
        lock (_nodePropertiesUpdateSync)
        {
            if (_nodePropertiesDebounceCtsByNodeId.TryGetValue(nodeId, out var previousCts))
                previousCts.Cancel();

            _nodePropertiesUpdateVersionsByNodeId.TryGetValue(nodeId, out var previousVersion);
            var version = previousVersion + 1;

            cts = new CancellationTokenSource();
            _nodePropertiesUpdateVersionsByNodeId[nodeId] = version;
            _nodePropertiesDebounceCtsByNodeId[nodeId] = cts;
            return version;
        }
    }

    private bool IsLatestNodePropertiesUpdate(string nodeId, long version)
    {
        lock (_nodePropertiesUpdateSync)
        {
            return _nodePropertiesUpdateVersionsByNodeId.TryGetValue(nodeId, out var currentVersion) &&
                currentVersion == version;
        }
    }

    private void CompleteNodePropertiesUpdate(string nodeId, long version, CancellationTokenSource cts)
    {
        lock (_nodePropertiesUpdateSync)
        {
            if (_nodePropertiesDebounceCtsByNodeId.TryGetValue(nodeId, out var currentCts) &&
                ReferenceEquals(currentCts, cts) &&
                _nodePropertiesUpdateVersionsByNodeId.TryGetValue(nodeId, out var currentVersion) &&
                currentVersion == version)
            {
                _nodePropertiesDebounceCtsByNodeId.Remove(nodeId);
            }
        }

        cts.Dispose();
    }

    private static int ReadIntSetting(string key, int defaultValue)
    {
        try
        {
            var values = ApplicationData.Current.LocalSettings.Values;
            if (values.TryGetValue(key, out var rawValue))
            {
                return rawValue switch
                {
                    int intValue => intValue,
                    long longValue when longValue is >= int.MinValue and <= int.MaxValue => (int)longValue,
                    string stringValue when int.TryParse(stringValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) => parsed,
                    _ => defaultValue
                };
            }
        }
        catch
        {
            // Ignore setting read failures and fall back to defaults.
        }

        return defaultValue;
    }

    private static void WriteIntSetting(string key, int value)
    {
        try
        {
            ApplicationData.Current.LocalSettings.Values[key] = value;
        }
        catch
        {
            // Ignore setting persistence failures.
        }
    }
}
