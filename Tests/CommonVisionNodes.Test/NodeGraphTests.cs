namespace CommonVisionNodes.Test
{
    public class NodeGraphTests
    {
        [Test]
        public void AddNode_ShouldAddNodeToGraph()
        {
            // Arrange
            var graph = new NodeGraph();
            var node = new ImageNode();

            // Act
            graph.AddNode(node);

            // Assert
            Assert.That(graph.Nodes, Does.Contain(node));
        }

        [Test]
        public void Connect_ShouldCreateConnectionBetweenPorts()
        {
            // Arrange
            var graph = new NodeGraph();
            var node1 = new ImageNode();
            var node2 = new SaveImageNode();
            graph.AddNode(node1);
            graph.AddNode(node2);

            var outputPort = node1.Outputs.First();
            var inputPort = node2.Inputs.First();

            // Act
            graph.Connect(outputPort, inputPort);

            // Assert
            var connection = graph.Connections.FirstOrDefault();
            Assert.That(connection, Is.Not.Null);
            Assert.That(outputPort, Is.EqualTo(connection.Output));
            Assert.That(inputPort, Is.EqualTo(connection.Input));
        }

        [Test]
        public void Connect_ShouldThrowExceptionForInvalidPortDirection()
        {
            // Arrange
            var graph = new NodeGraph();
            var node1 = new ImageNode();
            var node2 = new SaveImageNode();
            graph.AddNode(node1);
            graph.AddNode(node2);

            var invalidOutputPort = node2.Inputs.First();
            var inputPort = node1.Outputs.First();

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => graph.Connect(invalidOutputPort, inputPort));
        }

        [Test]
        public void Connect_ShouldThrowExceptionForSelfConnection()
        {
            // Arrange
            var graph = new NodeGraph();
            var node = new PassthroughNode();
            graph.AddNode(node);

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => graph.Connect(node.Output, node.Input));
        }

        [Test]
        public void Connect_ShouldThrowExceptionForDuplicateConnection()
        {
            // Arrange
            var graph = new NodeGraph();
            var node1 = new ImageNode();
            var node2 = new SaveImageNode();
            graph.AddNode(node1);
            graph.AddNode(node2);

            var outputPort = node1.Outputs.First();
            var inputPort = node2.Inputs.First();

            graph.Connect(outputPort, inputPort);

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => graph.Connect(outputPort, inputPort));
        }

        [Test]
        public void Execute_SingleNode_ShouldExecuteNode()
        {
            // Arrange
            var graph = new NodeGraph();
            var source = new SourceNode { ProducedValue = "hello" };
            graph.AddNode(source);

            // Act
            graph.Execute();

            // Assert
            Assert.That(source.Output.Value, Is.EqualTo("hello"));
        }

        [Test]
        public void Execute_LinearChain_ShouldPropagateData()
        {
            // Arrange
            var graph = new NodeGraph();
            var source = new SourceNode { ProducedValue = 42 };
            var sink = new SinkNode();
            graph.AddNode(source);
            graph.AddNode(sink);
            graph.Connect(source.Output, sink.Input);

            // Act
            graph.Execute();

            // Assert
            Assert.That(sink.ReceivedValue, Is.EqualTo(42));
        }

        [Test]
        public void Execute_LinearChain_ShouldExecuteInOrder()
        {
            // Arrange
            var executionOrder = new List<Node>();
            var graph = new NodeGraph();
            var source = new SourceNode { ProducedValue = 1, ExecutionLog = executionOrder };
            var sink = new SinkNode { ExecutionLog = executionOrder };
            graph.AddNode(sink);
            graph.AddNode(source);
            graph.Connect(source.Output, sink.Input);

            // Act
            graph.Execute();

            // Assert
            Assert.That(executionOrder, Has.Count.EqualTo(2));
            Assert.That(executionOrder[0], Is.SameAs(source));
            Assert.That(executionOrder[1], Is.SameAs(sink));
        }

        [Test]
        public void Execute_ThreeNodeChain_ShouldPropagateDataThroughMiddle()
        {
            // Arrange
            var graph = new NodeGraph();
            var source = new SourceNode { ProducedValue = 10 };
            var passthrough = new PassthroughNode { Transform = v => (int)v! * 2 };
            var sink = new SinkNode();
            graph.AddNode(source);
            graph.AddNode(passthrough);
            graph.AddNode(sink);
            graph.Connect(source.Output, passthrough.Input);
            graph.Connect(passthrough.Output, sink.Input);

            // Act
            graph.Execute();

            // Assert
            Assert.That(sink.ReceivedValue, Is.EqualTo(20));
        }

        [Test]
        public void Execute_BranchingGraph_ShouldPropagateToMultipleSinks()
        {
            // Arrange
            var graph = new NodeGraph();
            var source = new SourceNode { ProducedValue = "shared" };
            var sink1 = new SinkNode();
            var sink2 = new SinkNode();
            graph.AddNode(source);
            graph.AddNode(sink1);
            graph.AddNode(sink2);
            graph.Connect(source.Output, sink1.Input);
            graph.Connect(source.Output, sink2.Input);

            // Act
            graph.Execute();

            // Assert
            Assert.That(sink1.ReceivedValue, Is.EqualTo("shared"));
            Assert.That(sink2.ReceivedValue, Is.EqualTo("shared"));
        }

        [Test]
        public void Execute_DisconnectedInput_ShouldHaveNullValue()
        {
            // Arrange
            var graph = new NodeGraph();
            var sink = new SinkNode();
            graph.AddNode(sink);

            // Act
            graph.Execute();

            // Assert
            Assert.That(sink.ReceivedValue, Is.Null);
        }

        [Test]
        public void Execute_CyclicGraph_ShouldThrowException()
        {
            // Arrange
            var graph = new NodeGraph();
            var a = new PassthroughNode();
            var b = new PassthroughNode();
            graph.AddNode(a);
            graph.AddNode(b);
            graph.Connect(a.Output, b.Input);
            graph.Connect(b.Output, a.Input);

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => graph.Execute());
        }

        [Test]
        public void Execute_DiamondGraph_ShouldExecuteEachNodeOnce()
        {
            // Arrange
            var executionOrder = new List<Node>();
            var graph = new NodeGraph();
            var source = new SourceNode { ProducedValue = 1, ExecutionLog = executionOrder };
            var left = new PassthroughNode { ExecutionLog = executionOrder };
            var right = new PassthroughNode { ExecutionLog = executionOrder };
            var sink = new DualInputSinkNode { ExecutionLog = executionOrder };
            graph.AddNode(source);
            graph.AddNode(left);
            graph.AddNode(right);
            graph.AddNode(sink);
            graph.Connect(source.Output, left.Input);
            graph.Connect(source.Output, right.Input);
            graph.Connect(left.Output, sink.Input1);
            graph.Connect(right.Output, sink.Input2);

            // Act
            graph.Execute();

            // Assert
            Assert.That(executionOrder, Has.Count.EqualTo(4));
            Assert.That(executionOrder[0], Is.SameAs(source));
            Assert.That(executionOrder[^1], Is.SameAs(sink));
        }

        [Test]
        public void Initialize_ShouldInitializeInitializableNodesInOrder()
        {
            // Arrange
            var initLog = new List<Node>();
            var graph = new NodeGraph();
            var source = new InitializableSourceNode { InitLog = initLog };
            var sink = new SinkNode();
            graph.AddNode(sink);
            graph.AddNode(source);
            graph.Connect(source.Output, sink.Input);

            // Act
            graph.Initialize();

            // Assert
            Assert.That(initLog, Has.Count.EqualTo(1));
            Assert.That(initLog[0], Is.SameAs(source));
            Assert.That(source.IsInitialized, Is.True);
        }

        [Test]
        public void Initialize_ShouldSkipNonInitializableNodes()
        {
            // Arrange
            var graph = new NodeGraph();
            var source = new SourceNode { ProducedValue = 1 };
            var sink = new SinkNode();
            graph.AddNode(source);
            graph.AddNode(sink);
            graph.Connect(source.Output, sink.Input);

            // Act & Assert — should not throw
            Assert.DoesNotThrow(() => graph.Initialize());
        }

        [Test]
        public void Initialize_ShouldSkipAlreadyInitializedNodes()
        {
            // Arrange
            var initLog = new List<Node>();
            var graph = new NodeGraph();
            var source = new InitializableSourceNode { InitLog = initLog };
            graph.AddNode(source);

            graph.Initialize();
            Assert.That(initLog, Has.Count.EqualTo(1));

            // Act — initialize again
            graph.Initialize();

            // Assert — should not have initialized again
            Assert.That(initLog, Has.Count.EqualTo(1));
        }

        [Test]
        public void Dispose_ShouldDisposeInitializableNodes()
        {
            // Arrange
            var graph = new NodeGraph();
            var source = new InitializableSourceNode();
            graph.AddNode(source);
            graph.Initialize();
            Assert.That(source.IsInitialized, Is.True);

            // Act
            graph.Dispose();

            // Assert
            Assert.That(source.IsInitialized, Is.False);
        }

        [Test]
        public void Dispose_ShouldNotAffectNonInitializableNodes()
        {
            // Arrange
            var graph = new NodeGraph();
            var source = new SourceNode { ProducedValue = 1 };
            graph.AddNode(source);

            // Act & Assert — should not throw
            Assert.DoesNotThrow(() => graph.Dispose());
        }

        [Test]
        public void FullLifecycle_InitializeExecuteDispose()
        {
            // Arrange
            var initLog = new List<Node>();
            var execLog = new List<Node>();
            var graph = new NodeGraph();
            var source = new InitializableSourceNode { InitLog = initLog, ExecutionLog = execLog };
            var sink = new SinkNode { ExecutionLog = execLog };
            graph.AddNode(source);
            graph.AddNode(sink);
            graph.Connect(source.Output, sink.Input);

            // Act
            graph.Initialize();
            graph.Execute();
            graph.Dispose();

            // Assert
            Assert.That(initLog, Has.Count.EqualTo(1));
            Assert.That(execLog, Has.Count.EqualTo(2));
            Assert.That(sink.ReceivedValue, Is.EqualTo("initialized"));
            Assert.That(source.IsInitialized, Is.False);
        }
    }

    internal sealed class SourceNode : Node
    {
        public Port Output { get; }
        public object? ProducedValue { get; set; }
        public List<Node>? ExecutionLog { get; set; }

        public SourceNode()
        {
            Output = AddOutput("Output", typeof(object));
        }

        public override void Execute()
        {
            Output.Value = ProducedValue;
            ExecutionLog?.Add(this);
        }
    }

    internal sealed class SinkNode : Node
    {
        public Port Input { get; }
        public object? ReceivedValue { get; private set; }
        public List<Node>? ExecutionLog { get; set; }

        public SinkNode()
        {
            Input = AddInput("Input", typeof(object));
        }

        public override void Execute()
        {
            ReceivedValue = Input.Value;
            ExecutionLog?.Add(this);
        }
    }

    internal sealed class PassthroughNode : Node
    {
        public Port Input { get; }
        public Port Output { get; }
        public Func<object?, object?>? Transform { get; set; }
        public List<Node>? ExecutionLog { get; set; }

        public PassthroughNode()
        {
            Input = AddInput("Input", typeof(object));
            Output = AddOutput("Output", typeof(object));
        }

        public override void Execute()
        {
            Output.Value = Transform != null ? Transform(Input.Value) : Input.Value;
            ExecutionLog?.Add(this);
        }
    }

    internal sealed class DualInputSinkNode : Node
    {
        public Port Input1 { get; }
        public Port Input2 { get; }
        public List<Node>? ExecutionLog { get; set; }

        public DualInputSinkNode()
        {
            Input1 = AddInput("Input1", typeof(object));
            Input2 = AddInput("Input2", typeof(object));
        }

        public override void Execute()
        {
            ExecutionLog?.Add(this);
        }
    }

    internal sealed class InitializableSourceNode : Node, IInitializable
    {
        public Port Output { get; }
        public bool IsInitialized { get; private set; }
        public List<Node>? InitLog { get; set; }
        public List<Node>? ExecutionLog { get; set; }

        public InitializableSourceNode()
        {
            Output = AddOutput("Output", typeof(object));
        }

        public void Initialize()
        {
            IsInitialized = true;
            InitLog?.Add(this);
        }

        public override void Execute()
        {
            if (!IsInitialized)
                throw new InvalidOperationException("Not initialized.");

            Output.Value = "initialized";
            ExecutionLog?.Add(this);
        }

        public void Dispose()
        {
            IsInitialized = false;
        }
    }
}