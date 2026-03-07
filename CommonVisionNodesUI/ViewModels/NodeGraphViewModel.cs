using System.Collections.ObjectModel;
using CommonVisionNodes;

namespace CommonVisionNodesUI.ViewModels;

public partial class NodeGraphViewModel : ObservableObject
{
    private readonly NodeGraph _graph = new();
    private double _nextNodeX = 50;
    private double _nextNodeY = 50;

    public ObservableCollection<NodeViewModel> Nodes { get; } = [];
    public ObservableCollection<ConnectionViewModel> Connections { get; } = [];

    [ObservableProperty]
    private NodeViewModel? _selectedNode;

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
        // Remove connections involving this node
        var toRemove = Connections
            .Where(c => c.Source.ParentNode == nodeVM || c.Target.ParentNode == nodeVM)
            .ToList();
        foreach (var c in toRemove)
            Connections.Remove(c);

        Nodes.Remove(nodeVM);
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
        foreach (var node in Nodes.OfType<ImageNodeViewModel>())
            node.RefreshPreview();
        foreach (var node in Nodes.OfType<SaveImageNodeViewModel>())
            node.RefreshPreview();
    }
}
