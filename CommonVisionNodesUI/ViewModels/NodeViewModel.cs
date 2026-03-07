using CommonVisionNodes;
using Windows.UI;

namespace CommonVisionNodesUI.ViewModels;

public partial class NodeViewModel : ObservableObject
{
    public const double NodeWidth = 200;
    public const double HeaderHeight = 36;
    public const double PortHeight = 28;

    public Node Node { get; }
    public string Title => Node.GetType().Name.Replace("Node", "");
    public List<PortViewModel> InputPorts { get; }
    public List<PortViewModel> OutputPorts { get; }

    public virtual string? Summary => null;

    [ObservableProperty]
    private double _x;

    [ObservableProperty]
    private double _y;

    [ObservableProperty]
    private bool _isSelected;

    public double Height => HeaderHeight + Math.Max(InputPorts.Count, OutputPorts.Count) * PortHeight + 8;

    public Color HeaderColor => Node switch
    {
        ImageNode => Color.FromArgb(255, 74, 144, 217),
        SaveImageNode => Color.FromArgb(255, 102, 187, 106),
        DeviceNode => Color.FromArgb(255, 171, 71, 188),
        BinarizeNode => Color.FromArgb(255, 255, 152, 0),
        _ => Color.FromArgb(255, 128, 128, 128),
    };

    public NodeViewModel(Node node, double x, double y)
    {
        Node = node;
        _x = x;
        _y = y;
        InputPorts = node.Inputs.Select((p, i) => new PortViewModel(p, this, i)).ToList();
        OutputPorts = node.Outputs.Select((p, i) => new PortViewModel(p, this, i)).ToList();
    }

    partial void OnXChanged(double value) => NotifyPortPositions();
    partial void OnYChanged(double value) => NotifyPortPositions();

    private void NotifyPortPositions()
    {
        foreach (var p in InputPorts) p.NotifyPositionChanged();
        foreach (var p in OutputPorts) p.NotifyPositionChanged();
    }
}
