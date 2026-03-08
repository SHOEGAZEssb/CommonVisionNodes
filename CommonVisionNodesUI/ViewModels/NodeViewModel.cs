using CommonVisionNodes;
using Windows.UI;

namespace CommonVisionNodesUI.ViewModels;

/// <summary>
/// Base view model for a visual node on the graph canvas.
/// Manages position, selection state, port layout, and header color.
/// </summary>
public abstract partial class NodeViewModel : ObservableObject
{
    /// <summary>
    /// Fixed width of every node control in pixels.
    /// </summary>
    public const double NodeWidth = 200;

    /// <summary>
    /// Height of the node header in pixels.
    /// </summary>
    public const double HeaderHeight = 36;

    /// <summary>
    /// Height of a single port row in pixels.
    /// </summary>
    public const double PortHeight = 28;

    /// <summary>
    /// The underlying domain node.
    /// </summary>
    public Node Node { get; }

    /// <summary>
    /// Display title derived from the node type name.
    /// </summary>
    public string Title => Node.GetType().Name.Replace("Node", "");

    /// <summary>
    /// View models for the node's input ports.
    /// </summary>
    public List<PortViewModel> InputPorts { get; }

    /// <summary>
    /// View models for the node's output ports.
    /// </summary>
    public List<PortViewModel> OutputPorts { get; }

    /// <summary>
    /// Optional short summary shown below the node header.
    /// </summary>
    public virtual string? Summary => null;

    /// <summary>
    /// Whether this node's properties can be changed while the graph is running.
    /// </summary>
    public virtual bool IsEditableWhileRunning => false;

    [ObservableProperty]
    private double _x;

    [ObservableProperty]
    private double _y;

    [ObservableProperty]
    private bool _isSelected;

    /// <summary>
    /// Total height of the node control based on port count.
    /// </summary>
    public double Height => HeaderHeight + Math.Max(InputPorts.Count, OutputPorts.Count) * PortHeight + 8;

    /// <summary>
    /// Color used for the node header, varies by node type.
    /// </summary>
    public Color HeaderColor => Node switch
    {
        ImageNode => Color.FromArgb(255, 74, 144, 217),
        SaveImageNode => Color.FromArgb(255, 102, 187, 106),
        DeviceNode => Color.FromArgb(255, 171, 71, 188),
        BinarizeNode => Color.FromArgb(255, 255, 152, 0),
        SubImageNode => Color.FromArgb(255, 0, 172, 193),
        MatrixTransformNode => Color.FromArgb(255, 233, 30, 99),
        _ => Color.FromArgb(255, 128, 128, 128),
    };

    /// <summary>
    /// Creates a new node view model.
    /// </summary>
    /// <param name="node">The underlying domain node.</param>
    /// <param name="x">Initial X position on the canvas.</param>
    /// <param name="y">Initial Y position on the canvas.</param>
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
