namespace CommonVisionNodes
{
    /// <summary>
    /// A directed acyclic graph of <see cref="Node"/> instances connected via <see cref="Port"/>s.
    /// Supports initialization, execution, and disposal in topological order.
    /// </summary>
    public sealed class NodeGraph : IDisposable
    {
        private readonly List<Node> _nodes = [];
        private readonly List<Connection> _connections = [];

        /// <summary>
        /// All nodes in the graph.
        /// </summary>
        public IReadOnlyList<Node> Nodes => _nodes;

        /// <summary>
        /// All connections between node ports.
        /// </summary>
        public IReadOnlyList<Connection> Connections => _connections;

        /// <summary>
        /// Adds a node to the graph.
        /// </summary>
        /// <param name="node">The node to add.</param>
        public void AddNode(Node node)
        {
            _nodes.Add(node);
        }

        /// <summary>
        /// Removes a node and all its connections from the graph.
        /// If the node implements <see cref="IInitializable"/>, it is disposed.
        /// </summary>
        /// <param name="node">The node to remove.</param>
        public void RemoveNode(Node node)
        {
            _connections.RemoveAll(c => c.Output.Node == node || c.Input.Node == node);
            _nodes.Remove(node);

            if (node is IInitializable initializable)
                initializable.Dispose();
        }

        /// <summary>
        /// Connects an output port to an input port.
        /// </summary>
        /// <param name="output">The source output port.</param>
        /// <param name="input">The destination input port.</param>
        /// <exception cref="InvalidOperationException">Thrown when port directions or types are incompatible.</exception>
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

        /// <summary>
        /// Initializes all <see cref="IInitializable"/> nodes that have not yet been initialized,
        /// in topological order.
        /// </summary>
        public void Initialize()
        {
            var sorted = TopologicalSort();
            foreach (var node in sorted)
            {
                if (node is IInitializable initializable && !initializable.IsInitialized)
                    initializable.Initialize();
            }
        }

        /// <summary>
        /// Executes all nodes in topological order, propagating values along connections.
        /// </summary>
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

        /// <summary>
        /// Disposes all <see cref="IInitializable"/> nodes in reverse topological order.
        /// </summary>
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
