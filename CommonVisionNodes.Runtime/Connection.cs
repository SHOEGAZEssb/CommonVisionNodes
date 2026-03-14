namespace CommonVisionNodes
{
    /// <summary>
    /// Represents a directed link from an output port to an input port.
    /// </summary>
    public sealed class Connection
    {
        /// <summary>
        /// The source output port.
        /// </summary>
        public Port Output { get; }

        /// <summary>
        /// The destination input port.
        /// </summary>
        public Port Input { get; }

        /// <summary>
        /// Creates a new connection between two ports.
        /// </summary>
        /// <param name="output">Source output port.</param>
        /// <param name="input">Destination input port.</param>
        public Connection(Port output, Port input)
        {
            Output = output;
            Input = input;
        }
    }
}
