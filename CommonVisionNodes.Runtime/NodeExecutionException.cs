namespace CommonVisionNodes
{
    /// <summary>
    /// Raised when a node throws while the graph is executing.
    /// </summary>
    public sealed class NodeExecutionException : Exception
    {
        /// <summary>
        /// Creates an exception that preserves the failing node and original error.
        /// </summary>
        /// <param name="node">Node that failed.</param>
        /// <param name="innerException">Original exception thrown by the node.</param>
        public NodeExecutionException(Node node, Exception innerException)
            : base($"Node '{node.GetType().Name}' execution failed.", innerException)
        {
            Node = node;
        }

        /// <summary>
        /// Node that failed during graph execution.
        /// </summary>
        public Node Node { get; }
    }
}
