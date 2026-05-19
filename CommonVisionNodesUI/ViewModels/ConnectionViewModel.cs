using CommonVisionNodes.Contracts;

namespace CommonVisionNodesUI.ViewModels;

/// <summary>
/// View model for a rendered connection between two ports.
/// </summary>
public class ConnectionViewModel
{
    /// <summary>
    /// Creates a connection view model.
    /// </summary>
    /// <param name="connection">Serialized connection DTO.</param>
    /// <param name="source">Source output port.</param>
    /// <param name="target">Target input port.</param>
    public ConnectionViewModel(ConnectionDto connection, PortViewModel source, PortViewModel target)
    {
        Connection = connection;
        Source = source;
        Target = target;
    }

    /// <summary>
    /// Serialized connection DTO.
    /// </summary>
    public ConnectionDto Connection { get; }

    /// <summary>
    /// Source output port.
    /// </summary>
    public PortViewModel Source { get; }

    /// <summary>
    /// Target input port.
    /// </summary>
    public PortViewModel Target { get; }
}
