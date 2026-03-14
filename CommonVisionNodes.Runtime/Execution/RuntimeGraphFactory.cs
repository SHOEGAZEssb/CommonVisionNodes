using CommonVisionNodes.Contracts;

namespace CommonVisionNodes.Runtime.Execution;

public sealed class RuntimeGraphFactory
{
    private readonly RuntimeNodeCatalog _catalog;

    public RuntimeGraphFactory(RuntimeNodeCatalog catalog)
    {
        _catalog = catalog;
    }

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

        foreach (var connectionDto in graphDto.Connections)
        {
            if (!nodesById.TryGetValue(connectionDto.OutputNodeId, out var outputNode))
                throw new InvalidOperationException($"Unknown output node '{connectionDto.OutputNodeId}'.");

            if (!nodesById.TryGetValue(connectionDto.InputNodeId, out var inputNode))
                throw new InvalidOperationException($"Unknown input node '{connectionDto.InputNodeId}'.");

            var outputPort = outputNode.Outputs.FirstOrDefault(port => string.Equals(port.Name, connectionDto.OutputPortName, StringComparison.OrdinalIgnoreCase));
            if (outputPort is null)
                throw new InvalidOperationException($"Unknown output port '{connectionDto.OutputPortName}' on node '{connectionDto.OutputNodeId}'.");

            var inputPort = inputNode.Inputs.FirstOrDefault(port => string.Equals(port.Name, connectionDto.InputPortName, StringComparison.OrdinalIgnoreCase));
            if (inputPort is null)
                throw new InvalidOperationException($"Unknown input port '{connectionDto.InputPortName}' on node '{connectionDto.InputNodeId}'.");

            graph.Connect(outputPort, inputPort);
        }

        return new RuntimeGraphBuildResult(graph, nodesById, nodeIdsByRuntime);
    }
}
