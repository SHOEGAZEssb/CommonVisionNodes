using CommonVisionNodes.Contracts;

namespace CommonVisionNodes.Runtime.Execution;

public sealed class RuntimeGraphBuildResult : IDisposable
{
    public RuntimeGraphBuildResult(
        NodeGraph graph,
        IReadOnlyDictionary<string, Node> nodesById,
        IReadOnlyDictionary<Node, string> nodeIdsByRuntime)
    {
        Graph = graph;
        NodesById = nodesById;
        NodeIdsByRuntime = nodeIdsByRuntime;
    }

    public NodeGraph Graph { get; }

    public IReadOnlyDictionary<string, Node> NodesById { get; }

    public IReadOnlyDictionary<Node, string> NodeIdsByRuntime { get; }

    public void Dispose() => Graph.Dispose();
}
