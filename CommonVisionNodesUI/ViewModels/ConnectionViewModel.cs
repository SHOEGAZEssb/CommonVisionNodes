using CommonVisionNodes;

namespace CommonVisionNodesUI.ViewModels;

/// <summary>
/// View model for a connection line drawn between two ports on the canvas.
/// </summary>
public class ConnectionViewModel
{
    /// <summary>
    /// The underlying domain connection.
    /// </summary>
    public Connection Connection { get; }

    /// <summary>
    /// The source (output) port view model.
    /// </summary>
    public PortViewModel Source { get; }

    /// <summary>
    /// The target (input) port view model.
    /// </summary>
    public PortViewModel Target { get; }

    /// <summary>
    /// Creates a new connection view model.
    /// </summary>
    /// <param name="connection">The underlying domain connection.</param>
    /// <param name="source">Source output port view model.</param>
    /// <param name="target">Target input port view model.</param>
    public ConnectionViewModel(Connection connection, PortViewModel source, PortViewModel target)
    {
        Connection = connection;
        Source = source;
        Target = target;
    }
}
