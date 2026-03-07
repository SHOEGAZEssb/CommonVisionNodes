namespace CommonVisionNodes
{
    public sealed class NodeGraph
    {
        private readonly List<Node> _nodes = [];
        private readonly List<Connection> _connections = [];

        public IReadOnlyList<Node> Nodes => _nodes;
        public IReadOnlyList<Connection> Connections => _connections;

        public void AddNode(Node node)
        {
            _nodes.Add(node);
        }

        public void Connect(Port output, Port input)
        {
            if (output.Direction != PortDirection.Output)
                throw new InvalidOperationException("Source must be output");

            if (input.Direction != PortDirection.Input)
                throw new InvalidOperationException("Target must be input");

            // todo possibly "manual" conversion for types later
            if (!input.Type.IsAssignableFrom(output.Type))
                throw new InvalidOperationException("Incompatible port types");

            _connections.Add(new Connection(output, input));
        }
    }
}
