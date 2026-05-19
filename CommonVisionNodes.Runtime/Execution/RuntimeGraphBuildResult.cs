namespace CommonVisionNodes.Runtime.Execution;

/// <summary>
/// Holds an executable runtime graph plus lookup maps between DTO ids and runtime nodes.
/// </summary>
public sealed class RuntimeGraphBuildResult : IDisposable
{
    /// <summary>
    /// Creates a runtime graph build result.
    /// </summary>
    /// <param name="graph">Executable graph.</param>
    /// <param name="nodesById">Map from serialized node id to runtime node.</param>
    /// <param name="nodeIdsByRuntime">Map from runtime node to serialized node id.</param>
    public RuntimeGraphBuildResult(
        NodeGraph graph,
        IReadOnlyDictionary<string, Node> nodesById,
        IReadOnlyDictionary<Node, string> nodeIdsByRuntime)
    {
        Graph = graph;
        NodesById = nodesById;
        NodeIdsByRuntime = nodeIdsByRuntime;
    }

    /// <summary>
    /// Executable graph instance.
    /// </summary>
    public NodeGraph Graph { get; }

    /// <summary>
    /// Lookup from serialized node id to runtime node.
    /// </summary>
    public IReadOnlyDictionary<string, Node> NodesById { get; }

    /// <summary>
    /// Lookup from runtime node to serialized node id.
    /// </summary>
    public IReadOnlyDictionary<Node, string> NodeIdsByRuntime { get; }

    /// <summary>
    /// Disposes the runtime graph and any initialized node resources.
    /// </summary>
    public void Dispose() => Graph.Dispose();
}
