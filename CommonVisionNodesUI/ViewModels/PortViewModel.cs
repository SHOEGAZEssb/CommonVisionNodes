using CommonVisionNodes.Contracts;

namespace CommonVisionNodesUI.ViewModels;

public partial class PortViewModel : ObservableObject
{
    public PortViewModel(PortDto port, NodeViewModel parentNode, int index)
    {
        Port = port;
        ParentNode = parentNode;
        Index = index;
    }

    public PortDto Port { get; }

    public NodeViewModel ParentNode { get; }

    public int Index { get; }

    public string TypeName => Port.Type;

    public string Tooltip
    {
        get
        {
            var header = $"{Port.Name} ({TypeName})";
            return string.IsNullOrEmpty(Port.Description) ? header : $"{header}\n{Port.Description}";
        }
    }

    public double CenterX => Port.Direction == PortDirectionDto.Input
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
