using System.Diagnostics;

namespace CommonVisionNodes.Runtime
{
	/// <summary>
	/// A directed acyclic graph of <see cref="Node"/> instances connected via <see cref="Port"/>s.
	/// Supports initialization, execution, and disposal in topological order.
	/// </summary>
	public sealed class NodeGraph : IDisposable
	{
		private readonly List<Node> _nodes = [];
		private readonly List<Connection> _connections = [];
		private List<Node>? _cachedSort;
		private Dictionary<Port, Connection>? _connectionLookup;

		/// <summary>
		/// Nodes currently contained in the graph.
		/// </summary>
		public IReadOnlyList<Node> Nodes => _nodes;

		/// <summary>
		/// Connections currently linking output ports to input ports.
		/// </summary>
		public IReadOnlyList<Connection> Connections => _connections;

		/// <summary>
		/// Adds a node to the graph and invalidates cached execution order.
		/// </summary>
		/// <param name="node">Node to add.</param>
		public void AddNode(Node node)
		{
			ArgumentNullException.ThrowIfNull(node);

			if (_nodes.Contains(node))
				throw new InvalidOperationException("Node already belongs to this graph.");

			_nodes.Add(node);
			InvalidateCache();
		}

		/// <summary>
		/// Removes a node, removes its connections, and disposes it when it owns initialized resources.
		/// </summary>
		/// <param name="node">Node to remove.</param>
		public void RemoveNode(Node node)
		{
			if (!_nodes.Remove(node))
				return;

			_connections.RemoveAll(c => c.Output.Node == node || c.Input.Node == node);
			InvalidateCache();

			if (node is IInitializable initializable)
				initializable.Dispose();
		}

		/// <summary>
		/// Removes a connection from the graph.
		/// </summary>
		/// <param name="connection">Connection to remove.</param>
		public void Disconnect(Connection connection)
		{
			_connections.Remove(connection);
			InvalidateCache();
		}

		/// <summary>
		/// Connects an output port to an input port after validating direction, type, and graph shape.
		/// </summary>
		/// <param name="output">Source output port.</param>
		/// <param name="input">Target input port.</param>
		/// <exception cref="InvalidOperationException">Thrown when the ports cannot be connected.</exception>
		public void Connect(Port output, Port input)
		{
			if (output.Direction != PortDirection.Output)
				throw new InvalidOperationException("Source must be output");

			if (input.Direction != PortDirection.Input)
				throw new InvalidOperationException("Target must be input");

			if (output.Node == input.Node)
				throw new InvalidOperationException("Cannot connect a node to itself");

			if (!_nodes.Contains(output.Node) || !_nodes.Contains(input.Node))
				throw new InvalidOperationException("Both ports must belong to nodes in this graph");

			if (_connections.Any(c => c.Output == output && c.Input == input))
				throw new InvalidOperationException("Connection already exists");

			if (_connections.Any(c => c.Input == input))
				throw new InvalidOperationException("Input port already has a connection");

			var outputType = ResolveOutputType(output);
			if (outputType is null || !input.Type.IsAssignableFrom(outputType))
				throw new InvalidOperationException("Incompatible port types");

			_connections.Add(new Connection(output, input));
			InvalidateCache();
		}

		/// <summary>
		/// Initializes all <see cref="IInitializable"/> nodes in topological order.
		/// </summary>
		public void Initialize(Action<Node>? beforeInitialize = null, Action<Node>? afterInitialize = null)
		{
			var sorted = TopologicalSort();
			foreach (var node in sorted)
			{
				if (node is IInitializable initializable && !initializable.IsInitialized)
				{
					beforeInitialize?.Invoke(node);
					try
					{
						initializable.Initialize();
					}
					catch (Exception exception)
					{
						throw new NodeExecutionException(node, exception);
					}

					afterInitialize?.Invoke(node);
				}
			}
		}

		/// <summary>
		/// Executes the graph once in topological order.
		/// </summary>
		/// <param name="beforeExecute">Optional callback invoked immediately before a node executes.</param>
		/// <param name="afterExecute">Optional callback invoked immediately after a node executes.</param>
		/// <exception cref="NodeExecutionException">Thrown when a node fails during execution.</exception>
		public void Execute(Action<Node>? beforeExecute = null, Action<Node>? afterExecute = null)
			=> ExecuteWithActivity(beforeExecute, afterExecute);

		/// <summary>
		/// Executes the graph and reports whether at least one non-trigger node ran.
		/// </summary>
		internal bool ExecuteWithActivity(Action<Node>? beforeExecute = null, Action<Node>? afterExecute = null)
			=> ExecuteWithActivity(beforeExecute, afterExecute, trackedNodes: null, executedTrackedNodes: null);

		/// <summary>
		/// Executes the graph, reports whether at least one non-trigger node ran, and records the
		/// successfully executed nodes in <paramref name="executedTrackedNodes"/>.
		/// </summary>
		internal bool ExecuteWithActivity(IReadOnlySet<Node> trackedNodes, ICollection<Node> executedTrackedNodes)
		{
			ArgumentNullException.ThrowIfNull(trackedNodes);
			ArgumentNullException.ThrowIfNull(executedTrackedNodes);
			executedTrackedNodes.Clear();
			return ExecuteWithActivity(beforeExecute: null, afterExecute: null, trackedNodes, executedTrackedNodes);
		}

		private bool ExecuteWithActivity(
			Action<Node>? beforeExecute,
			Action<Node>? afterExecute,
			IReadOnlySet<Node>? trackedNodes,
			ICollection<Node>? executedTrackedNodes)
		{
			var sorted = _cachedSort ??= TopologicalSort();
			var lookup = _connectionLookup ??= BuildConnectionLookup();
			var activeOutputs = new HashSet<Port>();
			var executedWork = false;

			foreach (var node in sorted)
			{
				foreach (var input in node.Inputs)
				{
					if (lookup.TryGetValue(input, out var connection) && activeOutputs.Contains(connection.Output))
						input.Value = connection.Output.Value;
				}

				if (!ShouldExecute(node, lookup, activeOutputs))
				{
					node.LastExecutionTime = TimeSpan.Zero;
					continue;
				}

				beforeExecute?.Invoke(node);

				var sw = Stopwatch.StartNew();
				try
				{
					node.Execute();
				}
				catch (Exception ex)
				{
					sw.Stop();
					node.LastExecutionTime = sw.Elapsed;
					throw new NodeExecutionException(node, ex);
				}

				sw.Stop();
				node.LastExecutionTime = sw.Elapsed;

				foreach (var output in node.Outputs)
					activeOutputs.Add(output);

				if (node is not TimeTriggerNode and not ManualTriggerNode)
					executedWork = true;

				if (trackedNodes?.Contains(node) == true)
					executedTrackedNodes?.Add(node);

				afterExecute?.Invoke(node);
			}

			return executedWork;
		}

		/// <summary>
		/// Disposes initialized nodes in reverse topological order.
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

		private void InvalidateCache()
		{
			_cachedSort = null;
			_connectionLookup = null;
		}

		/// <summary>
		/// Resolves the concrete type emitted by an output port. A generic visualizer's output
		/// takes the type of the port connected to its input, allowing it to remain in a typed pipeline.
		/// </summary>
		private Type? ResolveOutputType(Port output)
			=> ResolveOutputType(output, []);

		private Type? ResolveOutputType(Port output, HashSet<Port> visitedPorts)
		{
			if (!visitedPorts.Add(output))
				return null;

			if (output.Node is not GenericVisualizerNode visualizer || output != visualizer.DataOutput)
				return output.Type;

			var inputConnection = _connections.FirstOrDefault(connection => connection.Input == visualizer.DataInput);
			return inputConnection is null ? null : ResolveOutputType(inputConnection.Output, visitedPorts);
		}

		private Dictionary<Port, Connection> BuildConnectionLookup()
		{
			var lookup = new Dictionary<Port, Connection>(_connections.Count);
			foreach (var connection in _connections)
				lookup[connection.Input] = connection;
			return lookup;
		}

		private static bool ShouldExecute(
			Node node,
			Dictionary<Port, Connection> lookup,
			HashSet<Port> activeOutputs)
		{
			Port? triggerInput = null;
			if (node is ITriggerableNode triggerableNode && lookup.ContainsKey(triggerableNode.TriggerInput))
			{
				// Trigger inputs gate the node for the current frame only; inactive triggers keep
				// downstream nodes from seeing stale output values from previous graph executions.
				triggerInput = triggerableNode.TriggerInput;
				if (triggerInput.Value is not TriggerSignal { IsTriggered: true })
					return false;
			}

			foreach (var input in node.Inputs)
			{
				if (input == triggerInput)
					continue;

				if (lookup.TryGetValue(input, out var connection) && !activeOutputs.Contains(connection.Output))
					return false;
			}

			return true;
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
