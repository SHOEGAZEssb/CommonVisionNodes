using System.Diagnostics;

namespace CommonVisionNodes
{
    /// <summary>
    /// Raised when a node throws while the graph is executing.
    /// </summary>
    public sealed class NodeExecutionException : Exception
    {
        public NodeExecutionException(Node node, Exception innerException)
            : base($"Node '{node.GetType().Name}' execution failed.", innerException)
        {
            Node = node;
        }

        public Node Node { get; }
    }
}
