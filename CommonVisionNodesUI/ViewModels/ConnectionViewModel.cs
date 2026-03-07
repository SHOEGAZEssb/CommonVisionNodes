using CommonVisionNodes;

namespace CommonVisionNodesUI.ViewModels;

public class ConnectionViewModel
{
    public Connection Connection { get; }
    public PortViewModel Source { get; }
    public PortViewModel Target { get; }

    public ConnectionViewModel(Connection connection, PortViewModel source, PortViewModel target)
    {
        Connection = connection;
        Source = source;
        Target = target;
    }
}
