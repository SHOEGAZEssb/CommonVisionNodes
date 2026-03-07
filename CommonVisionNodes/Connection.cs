using System;
using System.Collections.Generic;
using System.Text;

namespace CommonVisionNodes
{
    public sealed class Connection
    {
        public Port Output { get; }
        public Port Input { get; }

        public Connection(Port output, Port input)
        {
            Output = output;
            Input = input;
        }
    }
}
