namespace CommonVisionNodes.Runtime
{
	/// <summary>
	/// Raised when a node throws while the graph is executing.
	/// </summary>
	/// <remarks>
	/// Creates an exception that preserves the failing node and original error.
	/// </remarks>
	/// <param name="node">Node that failed.</param>
	/// <param name="innerException">Original exception thrown by the node.</param>
	public sealed class NodeExecutionException(Node node, Exception innerException) : Exception($"Node '{node.GetType().Name}' execution failed.", innerException)
	{

		/// <summary>
		/// Node that failed during graph execution.
		/// </summary>
		public Node Node { get; } = node;
	}
}
