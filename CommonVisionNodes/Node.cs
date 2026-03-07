using System;
using System.Collections.Generic;
using System.Text;

namespace CommonVisionNodes
{
    public abstract class Node
    {
        public Guid Id { get; } = Guid.NewGuid();

        private readonly List<Port> _inputs = [];
        private readonly List<Port> _outputs = [];

        public IReadOnlyList<Port> Inputs => _inputs;
        public IReadOnlyList<Port> Outputs => _outputs;

        protected Port AddInput(string name, Type type)
        {
            var port = new Port(this, name, type, PortDirection.Input);
            _inputs.Add(port);
            return port;
        }

        protected Port AddOutput(string name, Type type)
        {
            var port = new Port(this, name, type, PortDirection.Output);
            _outputs.Add(port);
            return port;
        }

        public abstract void Execute();
    }
}
