using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using CommonVisionNodes;
using Microsoft.UI.Dispatching;

namespace CommonVisionNodesUI.ViewModels;

/// <summary>
/// View model for the node graph. Manages nodes, connections,
/// execution, continuous run loop, and FPS tracking.
/// </summary>
public partial class NodeGraphViewModel : ObservableObject
{
    private readonly NodeGraph _graph = new();
    private readonly DispatcherQueue _dispatcherQueue;
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private CancellationTokenSource? _runCts;
    private double _nextNodeX = 50;
    private double _nextNodeY = 50;

    public ObservableCollection<NodeViewModel> Nodes { get; } = [];
    public ObservableCollection<ConnectionViewModel> Connections { get; } = [];

    [ObservableProperty]
    private NodeViewModel? _selectedNode;

    [ObservableProperty]
    private bool _isRunning;

    [ObservableProperty]
    private double _fps;

    public NodeGraphViewModel()
    {
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
    }

    /// <summary>
    /// Selects a node, deselecting any previously selected node.
    /// </summary>
    /// <param name="node">The node to select, or <c>null</c> to clear the selection.</param>
    public void SelectNode(NodeViewModel? node)
    {
        if (SelectedNode != null)
            SelectedNode.IsSelected = false;
        SelectedNode = node;
        if (node != null)
            node.IsSelected = true;
    }

    [RelayCommand]
    private void AddImageNode() => AddNode(new ImageNode(), (n, x, y) => new ImageNodeViewModel((ImageNode)n, x, y));

    [RelayCommand]
    private void AddSaveImageNode() => AddNode(new SaveImageNode(), (n, x, y) => new SaveImageNodeViewModel((SaveImageNode)n, x, y));

    [RelayCommand]
    private void AddDeviceNode() => AddNode(new DeviceNode(), (n, x, y) => new DeviceNodeViewModel((DeviceNode)n, x, y));

    [RelayCommand]
    private void AddBinarizeNode() => AddNode(new BinarizeNode(), (n, x, y) => new BinarizeNodeViewModel((BinarizeNode)n, x, y));

    [RelayCommand]
    private void AddSubImageNode() => AddNode(new SubImageNode(), (n, x, y) => new SubImageNodeViewModel((SubImageNode)n, x, y));

    [RelayCommand]
    private void AddMatrixTransformNode() => AddNode(new MatrixTransformNode(), (n, x, y) => new MatrixTransformNodeViewModel((MatrixTransformNode)n, x, y));

    [RelayCommand]
    private void AddImageGeneratorNode() => AddNode(new ImageGeneratorNode(), (n, x, y) => new ImageGeneratorNodeViewModel((ImageGeneratorNode)n, x, y));

    [RelayCommand]
    private void AddFilterNode() => AddNode(new FilterNode(), (n, x, y) => new FilterNodeViewModel((FilterNode)n, x, y));

    [RelayCommand]
    private void AddHistogramNode() => AddNode(new HistogramNode(), (n, x, y) => new HistogramNodeViewModel((HistogramNode)n, x, y));

    [RelayCommand]
    private void AddMorphologyNode() => AddNode(new MorphologyNode(), (n, x, y) => new MorphologyNodeViewModel((MorphologyNode)n, x, y));

    [RelayCommand]
    private void AddBlobNode() => AddNode(new BlobNode(), (n, x, y) => new BlobNodeViewModel((BlobNode)n, x, y));

    [RelayCommand]
    private void AddNormalizeNode() => AddNode(new NormalizeNode(), (n, x, y) => new NormalizeNodeViewModel((NormalizeNode)n, x, y));

    [RelayCommand]
    private void AddPolimagoClassifyNode() => AddNode(new PolimagoClassifyNode(), (n, x, y) => new PolimagoClassifyNodeViewModel((PolimagoClassifyNode)n, x, y));

    private void AddNode(Node node, Func<Node, double, double, NodeViewModel> createVM)
    {
        _graph.AddNode(node);
        var vm = createVM(node, _nextNodeX, _nextNodeY);
        Nodes.Add(vm);
        _nextNodeX += 60;
        if (_nextNodeX > 500)
        {
            _nextNodeX = 50;
            _nextNodeY += 120;
        }
    }

    /// <summary>
    /// Attempts to connect two ports. One must be an output and the other an input.
    /// </summary>
    /// <param name="portA">First port.</param>
    /// <param name="portB">Second port.</param>
    /// <returns><c>true</c> if the connection was created successfully.</returns>
    public bool TryConnect(PortViewModel portA, PortViewModel portB)
    {
        try
        {
            var outputPort = portA.Port.Direction == PortDirection.Output ? portA : portB;
            var inputPort = portA.Port.Direction == PortDirection.Input ? portA : portB;

            if (outputPort.Port.Direction != PortDirection.Output ||
                inputPort.Port.Direction != PortDirection.Input)
                return false;

            _graph.Connect(outputPort.Port, inputPort.Port);
            var connection = _graph.Connections[^1];
            Connections.Add(new ConnectionViewModel(connection, outputPort, inputPort));
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Removes all connections that involve the given port.
    /// </summary>
    /// <param name="port">The port whose connections should be removed.</param>
    public void DisconnectPort(PortViewModel port)
    {
        var toRemove = Connections
            .Where(c => c.Source == port || c.Target == port)
            .ToList();
        foreach (var c in toRemove)
        {
            _graph.Disconnect(c.Connection);
            Connections.Remove(c);
        }
    }

    [RelayCommand]
    private void RemoveNode(NodeViewModel nodeVM)
    {
        var toRemove = Connections
            .Where(c => c.Source.ParentNode == nodeVM || c.Target.ParentNode == nodeVM)
            .ToList();
        foreach (var c in toRemove)
            Connections.Remove(c);

        _graph.RemoveNode(nodeVM.Node);
        Nodes.Remove(nodeVM);

        if (SelectedNode == nodeVM)
            SelectNode(null);
    }

    [RelayCommand]
    private void RemoveSelectedNode()
    {
        if (SelectedNode is { } node)
            RemoveNode(node);
    }

    [RelayCommand]
    private void InitializeGraph()
    {
        _graph.Initialize();
        foreach (var node in Nodes)
            node.RefreshPreview();
    }

    [RelayCommand]
    private void ExecuteGraph()
    {
        _graph.Execute();
        RefreshPreviews();
    }

    [RelayCommand]
    private void ToggleRun()
    {
        if (IsRunning)
            Stop();
        else
            Start();
    }

    private void Start()
    {
        if (IsRunning) return;
        _runCts = new CancellationTokenSource();
        IsRunning = true;
        var ct = _runCts.Token;

        Task.Run(async () =>
        {
            var fpsStopwatch = Stopwatch.StartNew();
            int frameCount = 0;

            try
            {
                while (!ct.IsCancellationRequested)
                {
                    await _refreshGate.WaitAsync(ct);
                    try
                    {
                        _graph.Execute();
                    }
                    catch
                    {
                        _refreshGate.Release();
                        throw;
                    }
                    frameCount++;

                    double? fpsToReport = null;
                    if (fpsStopwatch.ElapsedMilliseconds >= 1000)
                    {
                        fpsToReport = frameCount * 1000.0 / fpsStopwatch.ElapsedMilliseconds;
                        frameCount = 0;
                        fpsStopwatch.Restart();
                    }

                    _dispatcherQueue.TryEnqueue(() =>
                    {
                        try
                        {
                            if (fpsToReport.HasValue)
                                Fps = fpsToReport.Value;
                            RefreshPreviews();
                        }
                        finally { _refreshGate.Release(); }
                    });
                }
            }
            catch (Exception) when (ct.IsCancellationRequested)
            {
                // Expected on cancellation
            }
            catch (Exception)
            {
                _dispatcherQueue.TryEnqueue(Stop);
            }
        }, ct);
    }

    private void Stop()
    {
        _runCts?.Cancel();
        _runCts?.Dispose();
        _runCts = null;
        IsRunning = false;
        Fps = 0;
    }

    /// <summary>
    /// Adds a pre-created node and its view model to the graph.
    /// Used during deserialization to restore saved nodes.
    /// </summary>
    /// <param name="node">The domain node.</param>
    /// <param name="vm">The corresponding view model.</param>
    public void AddLoadedNode(Node node, NodeViewModel vm)
    {
        _graph.AddNode(node);
        Nodes.Add(vm);
    }

    /// <summary>
    /// Removes all nodes and connections, disposing the underlying graph.
    /// </summary>
    public void ClearGraph()
    {
        if (IsRunning)
            ToggleRun();

        SelectNode(null);
        Connections.Clear();

        // Remove nodes in reverse to avoid collection-modified issues
        for (int i = Nodes.Count - 1; i >= 0; i--)
        {
            _graph.RemoveNode(Nodes[i].Node);
            Nodes.RemoveAt(i);
        }

        _nextNodeX = 50;
        _nextNodeY = 50;
    }

    /// <summary>
    /// Generates standalone C# source code that replicates the current graph.
    /// </summary>
    /// <returns>A C# code snippet as a string.</returns>
    public string GenerateCode() => CodeGenerator.Generate(_graph);

    private void RefreshPreviews()
    {
        foreach (var node in Nodes)
        {
            node.RefreshExecutionTime();
            node.RefreshPreview();
        }
    }
}
