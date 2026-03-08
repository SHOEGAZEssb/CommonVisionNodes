using CommonVisionNodes;

namespace CommonVisionNodesUI.ViewModels;

/// <summary>
/// View model for a single port on a node, providing position information for rendering.
/// </summary>
public partial class PortViewModel : ObservableObject
{
    /// <summary>
    /// The underlying domain port.
    /// </summary>
    public Port Port { get; }

    /// <summary>
    /// The node view model that owns this port.
    /// </summary>
    public NodeViewModel ParentNode { get; }

    /// <summary>
    /// Zero-based index of this port in its direction group.
    /// </summary>
    public int Index { get; }

    /// <summary>
    /// Creates a new port view model.
    /// </summary>
    /// <param name="port">The underlying domain port.</param>
    /// <param name="parentNode">The owning node view model.</param>
    /// <param name="index">Zero-based index within the input or output group.</param>
    public PortViewModel(Port port, NodeViewModel parentNode, int index)
    {
        Port = port;
        ParentNode = parentNode;
        Index = index;
    }

    /// <summary>
    /// Center X coordinate of the port circle on the canvas.
    /// </summary>
    public double CenterX => Port.Direction == PortDirection.Input
        ? ParentNode.X + 10
        : ParentNode.X + NodeViewModel.NodeWidth - 10;

    /// <summary>
    /// Center Y coordinate of the port circle on the canvas.
    /// </summary>
    public double CenterY =>
        ParentNode.Y + NodeViewModel.HeaderHeight + Index * NodeViewModel.PortHeight + NodeViewModel.PortHeight / 2;

    /// <summary>
    /// Notifies bindings that the port's canvas position has changed.
    /// </summary>
    public void NotifyPositionChanged()
    {
        OnPropertyChanged(nameof(CenterX));
        OnPropertyChanged(nameof(CenterY));
    }
}
