namespace CommonVisionNodes
{
    public sealed class NodeGraph : IDisposable
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

        public void Initialize()
        {
            var sorted = TopologicalSort();
            foreach (var node in sorted)
            {
                if (node is IInitializable initializable && !initializable.IsInitialized)
                    initializable.Initialize();
            }
        }

        public void Execute()
        {
            var sorted = TopologicalSort();
            foreach (var node in sorted)
            {
                foreach (var input in node.Inputs)
                {
                    var connection = _connections.FirstOrDefault(c => c.Input == input);
                    if (connection != null)
                        input.Value = connection.Output.Value;
                }

                node.Execute();
            }
        }

        public void Dispose()
        {
            var sorted = TopologicalSort();
            sorted.Reverse();
            foreach (var node in sorted)
            {
                if (node is IInitializable initializable)
                    initializable.Dispose();
            }
        }

        private List<Node> TopologicalSort()
        {
            var inDegree = new Dictionary<Node, int>();
            var adjacency = new Dictionary<Node, List<Node>>();

            foreach (var node in _nodes)
            {
                inDegree[node] = 0;
                adjacency[node] = [];
            }

            foreach (var connection in _connections)
            {
                var from = connection.Output.Node;
                var to = connection.Input.Node;
                adjacency[from].Add(to);
                inDegree[to]++;
            }

            var queue = new Queue<Node>();
            foreach (var node in _nodes)
            {
                if (inDegree[node] == 0)
                    queue.Enqueue(node);
            }

            var sorted = new List<Node>();
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                sorted.Add(current);

                foreach (var neighbor in adjacency[current])
                {
                    inDegree[neighbor]--;
                    if (inDegree[neighbor] == 0)
                        queue.Enqueue(neighbor);
                }
            }

            if (sorted.Count != _nodes.Count)
                throw new InvalidOperationException("Graph contains a cycle");

            return sorted;
        }
    }
}
