using CommonVisionNodes;

namespace CommonVisionNodesUI.ViewModels;

public partial class PortViewModel : ObservableObject
{
    public Port Port { get; }
    public NodeViewModel ParentNode { get; }
    public int Index { get; }

    public PortViewModel(Port port, NodeViewModel parentNode, int index)
    {
        Port = port;
        ParentNode = parentNode;
        Index = index;
    }

    public double CenterX => Port.Direction == PortDirection.Input
        ? ParentNode.X + 10
        : ParentNode.X + NodeViewModel.NodeWidth - 10;

    public double CenterY =>
        ParentNode.Y + NodeViewModel.HeaderHeight + Index * NodeViewModel.PortHeight + NodeViewModel.PortHeight / 2;

    public void NotifyPositionChanged()
    {
        OnPropertyChanged(nameof(CenterX));
        OnPropertyChanged(nameof(CenterY));
    }
}
