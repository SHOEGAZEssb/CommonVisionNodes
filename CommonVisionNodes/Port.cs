namespace CommonVisionNodes
{
    /// <summary>
    /// Specifies whether a port is an input or an output.
    /// </summary>
    public enum PortDirection
    {
        /// <summary>
        /// Port receives data from another node.
        /// </summary>
        Input,
        /// <summary>
        /// Port provides data to another node.
        /// </summary>
        Output
    }

    /// <summary>
    /// A typed data endpoint on a <see cref="Node"/> used to pass values between nodes.
    /// </summary>
    public sealed class Port
    {
        /// <summary>
        /// The node that owns this port.
        /// </summary>
        public Node Node { get; }

        /// <summary>
        /// Display name of the port.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// The data type this port carries.
        /// </summary>
        public Type Type { get; }

        /// <summary>
        /// Whether this port is an input or an output.
        /// </summary>
        public PortDirection Direction { get; }

        /// <summary>
        /// The current value held by this port.
        /// </summary>
        public object? Value { get; set; }

        /// <summary>
        /// Creates a new port.
        /// </summary>
        /// <param name="node">Owning node.</param>
        /// <param name="name">Display name.</param>
        /// <param name="type">Data type carried by the port.</param>
        /// <param name="direction">Input or output.</param>
        internal Port(Node node, string name, Type type, PortDirection direction)
        {
            Node = node;
            Name = name;
            Type = type;
            Direction = direction;
        }
    }
}
