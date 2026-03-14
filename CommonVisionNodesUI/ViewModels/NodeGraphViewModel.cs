using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using CommonVisionNodes.Contracts;
using CommonVisionNodesUI.Services;
using Microsoft.UI.Dispatching;

namespace CommonVisionNodesUI.ViewModels;

public partial class NodeGraphViewModel : ObservableObject
{
    private readonly IBackendClient _backendClient;
    private readonly DispatcherQueue _dispatcherQueue;
    private readonly Dictionary<string, NodeDefinitionDto> _nodeDefinitions = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, NodeViewModel> _nodesById = new(StringComparer.OrdinalIgnoreCase);
    private readonly string _clientId = Guid.NewGuid().ToString("N");
    private CancellationTokenSource? _listenerCts;
    private Task? _listenerTask;
    private double _nextNodeX = 50;
    private double _nextNodeY = 50;
    private bool _initialized;

    public NodeGraphViewModel(IBackendClient backendClient)
    {
        _backendClient = backendClient;
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
    }

    public ObservableCollection<NodeViewModel> Nodes { get; } = [];

    public ObservableCollection<ConnectionViewModel> Connections { get; } = [];

    [ObservableProperty]
    private NodeViewModel? _selectedNode;

    [ObservableProperty]
    private bool _isRunning;

    [ObservableProperty]
    private double _fps;

    [ObservableProperty]
    private string _lastExecutionTimeText = "-";

    [ObservableProperty]
    private int _previewRefreshRate = 30;

    public string PreviewRefreshRateText => PreviewRefreshRate >= 1001 ? "inf" : PreviewRefreshRate.ToString(CultureInfo.InvariantCulture);

    public async Task InitializeAsync()
    {
        if (_initialized)
            return;

        await RefreshNodeDefinitionsAsync();

        _listenerCts = new CancellationTokenSource();
        _listenerTask = Task.Run(() => _backendClient.ListenAsync(_clientId, HandleExecutionMessageAsync, _listenerCts.Token));
        _initialized = true;
    }

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

    public void SelectNode(NodeViewModel? node)
    {
        if (SelectedNode is not null)
            SelectedNode.IsSelected = false;

        SelectedNode = node;
        if (node is not null)
            node.IsSelected = true;
    }

    public GraphDto ToGraphDto()
        => new()
        {
            Nodes = Nodes.Select(node => node.ToNodeDtoClone()).ToList(),
            Connections = Connections.Select(connection => new ConnectionDto
            {
                OutputNodeId = connection.Connection.OutputNodeId,
                OutputPortName = connection.Connection.OutputPortName,
                InputNodeId = connection.Connection.InputNodeId,
                InputPortName = connection.Connection.InputPortName
            }).ToList()
        };

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

    public void DisconnectPort(PortViewModel port)
    {
        var toRemove = Connections
            .Where(connection => connection.Source == port || connection.Target == port)
            .ToList();

        foreach (var connection in toRemove)
            Connections.Remove(connection);
    }

    [RelayCommand]
    private void AddImageNode() => AddNode("ImageNode");

    [RelayCommand]
    private void AddSaveImageNode() => AddNode("SaveImageNode");

    [RelayCommand]
    private void AddDeviceNode() => AddNode("DeviceNode");

    [RelayCommand]
    private void AddBinarizeNode() => AddNode("BinarizeNode");

    [RelayCommand]
    private void AddSubImageNode() => AddNode("SubImageNode");

    [RelayCommand]
    private void AddMatrixTransformNode() => AddNode("MatrixTransformNode");

    [RelayCommand]
    private void AddImageGeneratorNode() => AddNode("ImageGeneratorNode");

    [RelayCommand]
    private void AddFilterNode() => AddNode("FilterNode");

    [RelayCommand]
    private void AddHistogramNode() => AddNode("HistogramNode");

    [RelayCommand]
    private void AddMorphologyNode() => AddNode("MorphologyNode");

    [RelayCommand]
    private void AddBlobNode() => AddNode("BlobNode");

    [RelayCommand]
    private void AddNormalizeNode() => AddNode("NormalizeNode");

    [RelayCommand]
    private void AddPolimagoClassifyNode() => AddNode("PolimagoClassifyNode");

    [RelayCommand]
    private void AddGenericVisualizerNode() => AddNode("GenericVisualizerNode");

    [RelayCommand]
    private void AddCSharpNode() => AddNode("CSharpNode");

    [RelayCommand]
    private void RemoveNode(NodeViewModel nodeViewModel)
    {
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

        await _backendClient.ExecuteAsync(new ExecutionRequestDto
        {
            ClientId = _clientId,
            Graph = ToGraphDto(),
            Mode = ExecutionModeDto.Single,
            PreviewRefreshRate = PreviewRefreshRate
        });
    }

    [RelayCommand]
    private async Task ToggleRunAsync()
    {
        await InitializeAsync();

        if (IsRunning)
        {
            await _backendClient.StopAsync(_clientId);
            return;
        }

        await _backendClient.ExecuteAsync(new ExecutionRequestDto
        {
            ClientId = _clientId,
            Graph = ToGraphDto(),
            Mode = ExecutionModeDto.Continuous,
            PreviewRefreshRate = PreviewRefreshRate
        });
    }

    public void ClearGraph()
    {
        SelectNode(null);
        Connections.Clear();
        Nodes.Clear();
        _nodesById.Clear();
        _nextNodeX = 50;
        _nextNodeY = 50;
    }

    public Task<string> GenerateCodeAsync() => _backendClient.GenerateCodeAsync(ToGraphDto());

    public async ValueTask DisposeAsync()
    {
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
        OnPropertyChanged(nameof(PreviewRefreshRateText));
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
            Properties = definition.Properties.Select(property => new NodePropertyDto
            {
                Name = property.Name,
                Value = property.DefaultValue
            }).ToList()
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
        Nodes.Add(viewModel);
        _nodesById[viewModel.Node.Id] = viewModel;
    }

    private async Task HandleExecutionMessageAsync(ExecutionMessageDto message)
    {
        await EnqueueAsync(() => ApplyExecutionMessage(message));
    }

    private void ApplyExecutionMessage(ExecutionMessageDto message)
    {
        switch (message.MessageType)
        {
            case ExecutionMessageTypeDto.ExecutionState:
            case ExecutionMessageTypeDto.Completed:
            case ExecutionMessageTypeDto.Failure:
                ApplyExecutionState(message.ExecutionState);
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
}

