namespace CommonVisionNodes.Runtime
{
	/// <summary>
	/// Represents a directed link from an output port to an input port.
	/// </summary>
	/// <remarks>
	/// Creates a new connection between two ports.
	/// </remarks>
	/// <param name="output">Source output port.</param>
	/// <param name="input">Destination input port.</param>
	public sealed class Connection(Port output, Port input)
	{
		/// <summary>
		/// The source output port.
		/// </summary>
		public Port Output { get; } = output;

		/// <summary>
		/// The destination input port.
		/// </summary>
		public Port Input { get; } = input;
	}
}
