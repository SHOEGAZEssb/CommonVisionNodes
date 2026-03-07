using System.Xml.Linq;

namespace CommonVisionNodes
{
    public enum PortDirection
    {
        Input,
        Output
    }

    public sealed class Port
    {
        public Node Node { get; }
        public string Name { get; }
        public Type Type { get; }
        public PortDirection Direction { get; }

        internal Port(Node node, string name, Type type, PortDirection direction)
        {
            Node = node;
            Name = name;
            Type = type;
            Direction = direction;
        }
    }
}
