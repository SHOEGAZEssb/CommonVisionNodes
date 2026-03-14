using CommonVisionNodes.Contracts;

namespace CommonVisionNodesUI.ViewModels;

public class ConnectionViewModel
{
    public ConnectionViewModel(ConnectionDto connection, PortViewModel source, PortViewModel target)
    {
        Connection = connection;
        Source = source;
        Target = target;
    }

    public ConnectionDto Connection { get; }

    public PortViewModel Source { get; }

    public PortViewModel Target { get; }
}
