using CommonVisionNodes.Contracts;

namespace CommonVisionNodesUI.ViewModels;

/// <summary>
/// View model for one rendered node port.
/// </summary>
/// <remarks>
/// Creates a port view model.
/// </remarks>
/// <param name="port">Port metadata.</param>
/// <param name="parentNode">Node that owns the port.</param>
/// <param name="index">Zero-based port row index.</param>
public partial class PortViewModel(PortDto port, NodeViewModel parentNode, int index) : ObservableObject
{

	/// <summary>
	/// Port metadata.
	/// </summary>
	public PortDto Port { get; } = port;

	/// <summary>
	/// Node view model that owns the port.
	/// </summary>
	public NodeViewModel ParentNode { get; } = parentNode;

	/// <summary>
	/// Zero-based port row index.
	/// </summary>
	public int Index { get; } = index;

	/// <summary>
	/// Display type name for the port.
	/// </summary>
	public string TypeName => Port.Type;

    /// <summary>
    /// Tooltip text composed from the port name, type, and description.
    /// </summary>
    public string Tooltip
    {
        get
        {
            var header = $"{Port.Name} ({TypeName})";
            return string.IsNullOrEmpty(Port.Description) ? header : $"{header}\n{Port.Description}";
        }
    }

    /// <summary>
    /// X coordinate of the port center on the graph canvas.
    /// </summary>
    public double CenterX => Port.Direction == PortDirectionDto.Input
        ? ParentNode.X + 10
        : ParentNode.X + ParentNode.Width - 10;

    /// <summary>
    /// Y coordinate of the port center on the graph canvas.
    /// </summary>
    public double CenterY =>
        ParentNode.Y + NodeViewModel.HeaderHeight + Index * NodeViewModel.PortHeight + NodeViewModel.PortHeight / 2;

    /// <summary>
    /// Notifies bindings that the port center moved because the parent node moved.
    /// </summary>
    public void NotifyPositionChanged()
    {
        OnPropertyChanged(nameof(CenterX));
        OnPropertyChanged(nameof(CenterY));
    }
}
