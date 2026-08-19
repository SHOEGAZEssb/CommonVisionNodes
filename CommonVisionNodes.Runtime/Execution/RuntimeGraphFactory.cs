using CommonVisionNodes.Contracts;
using CommonVisionNodes.Runtime.Definitions;

namespace CommonVisionNodes.Runtime.Execution;

/// <summary>
/// Converts serialized graph DTOs into executable <see cref="NodeGraph"/> instances.
/// </summary>
/// <remarks>
/// Creates a runtime graph factory.
/// </remarks>
/// <param name="catalog">Catalog used to create node instances.</param>
public sealed class RuntimeGraphFactory(RuntimeNodeCatalog catalog)
{
	private readonly RuntimeNodeCatalog _catalog = catalog;

	/// <summary>
	/// Builds a runtime graph, applies node properties, and connects ports.
	/// </summary>
	/// <param name="graphDto">Serialized graph definition.</param>
	/// <returns>Build result containing the graph and id maps used during execution.</returns>
	/// <exception cref="InvalidOperationException">Thrown when nodes or connection endpoints are invalid.</exception>
	public RuntimeGraphBuildResult Build(GraphDto graphDto)
	{
		var graph = new NodeGraph();
		var nodesById = new Dictionary<string, Node>(StringComparer.OrdinalIgnoreCase);
		var nodeIdsByRuntime = new Dictionary<Node, string>();

		foreach (var nodeDto in graphDto.Nodes)
		{
			if (string.IsNullOrWhiteSpace(nodeDto.Id))
				throw new InvalidOperationException("Node ids are required.");

			if (!_catalog.TryCreateNode(nodeDto.Type, out var node))
				throw new InvalidOperationException($"Unknown node type '{nodeDto.Type}'.");

			RuntimeNodePropertyBinder.Apply(node, nodeDto.Properties);
			graph.AddNode(node);
			nodesById.Add(nodeDto.Id, node);
			nodeIdsByRuntime.Add(node, nodeDto.Id);
		}

		var pendingConnections = graphDto.Connections.ToList();
		while (pendingConnections.Count > 0)
		{
			var connectedAny = false;
			foreach (var connectionDto in pendingConnections.ToList())
			{
				try
				{
					Connect(graph, nodesById, connectionDto);
					pendingConnections.Remove(connectionDto);
					connectedAny = true;
				}
				catch (InvalidOperationException exception) when (exception.Message == "Incompatible port types")
				{
					// A dynamic pass-through output may need its own input connection first.
				}
			}

			if (!connectedAny)
				Connect(graph, nodesById, pendingConnections[0]);
		}

		return new RuntimeGraphBuildResult(graph, nodesById, nodeIdsByRuntime);
	}

	private static void Connect(NodeGraph graph, IReadOnlyDictionary<string, Node> nodesById, ConnectionDto connectionDto)
	{
		if (!nodesById.TryGetValue(connectionDto.OutputNodeId, out var outputNode))
			throw new InvalidOperationException($"Unknown output node '{connectionDto.OutputNodeId}'.");

		if (!nodesById.TryGetValue(connectionDto.InputNodeId, out var inputNode))
			throw new InvalidOperationException($"Unknown input node '{connectionDto.InputNodeId}'.");

		var outputPort = outputNode.Outputs.FirstOrDefault(port => string.Equals(port.Name, connectionDto.OutputPortName, StringComparison.OrdinalIgnoreCase)) ?? throw new InvalidOperationException($"Unknown output port '{connectionDto.OutputPortName}' on node '{connectionDto.OutputNodeId}'.");
		var inputPort = inputNode.Inputs.FirstOrDefault(port => string.Equals(port.Name, connectionDto.InputPortName, StringComparison.OrdinalIgnoreCase)) ?? throw new InvalidOperationException($"Unknown input port '{connectionDto.InputPortName}' on node '{connectionDto.InputNodeId}'.");
		graph.Connect(outputPort, inputPort);
	}
}
