using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using CommonVisionNodes;
using Microsoft.UI.Dispatching;

namespace CommonVisionNodesUI.ViewModels;

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

    public NodeGraphViewModel()
    {
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
    }

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
        foreach (var node in Nodes.OfType<ImageNodeViewModel>())
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
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    await _refreshGate.WaitAsync(ct);
                    _graph.Execute();
                    _dispatcherQueue.TryEnqueue(() =>
                    {
                        try { RefreshPreviews(); }
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
    }

    private void RefreshPreviews()
    {
        foreach (var node in Nodes.OfType<ImageNodeViewModel>())
            node.RefreshPreview();
        foreach (var node in Nodes.OfType<SaveImageNodeViewModel>())
            node.RefreshPreview();
        foreach (var node in Nodes.OfType<BinarizeNodeViewModel>())
            node.RefreshPreview();
        foreach (var node in Nodes.OfType<SubImageNodeViewModel>())
            node.RefreshPreview();
        foreach (var node in Nodes.OfType<MatrixTransformNodeViewModel>())
            node.RefreshPreview();
    }
}
