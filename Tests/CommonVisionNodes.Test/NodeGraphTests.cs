using CommonVisionNodes.Runtime;

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
		public void AddNode_WithSameInstanceTwice_ShouldThrow()
		{
			var graph = new NodeGraph();
			var node = new SourceNode();
			graph.AddNode(node);

			Assert.Throws<InvalidOperationException>(() => graph.AddNode(node));
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

			var outputPort = node1.Outputs[0];
			var inputPort = node2.Inputs[0];

			// Act
			graph.Connect(outputPort, inputPort);

			// Assert
			var connection = graph.Connections.FirstOrDefault();
			Assert.That(connection, Is.Not.Null);
			using (Assert.EnterMultipleScope())
			{
				Assert.That(outputPort, Is.EqualTo(connection.Output));
				Assert.That(inputPort, Is.EqualTo(connection.Input));
			}
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

			var invalidOutputPort = node2.Inputs[0];
			var inputPort = node1.Outputs[0];

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

			var outputPort = node1.Outputs[0];
			var inputPort = node2.Inputs[0];

			graph.Connect(outputPort, inputPort);

			// Act & Assert
			Assert.Throws<InvalidOperationException>(() => graph.Connect(outputPort, inputPort));
		}

		[Test]
		public void Connect_WithForeignNodePort_ShouldThrow()
		{
			var graph = new NodeGraph();
			var source = new SourceNode();
			var foreignSink = new SinkNode();
			graph.AddNode(source);

			Assert.Throws<InvalidOperationException>(() => graph.Connect(source.Output, foreignSink.Input));
		}

		[Test]
		public void Connect_WithSecondConnectionToSameInput_ShouldThrow()
		{
			var graph = new NodeGraph();
			var source1 = new SourceNode();
			var source2 = new SourceNode();
			var sink = new SinkNode();
			graph.AddNode(source1);
			graph.AddNode(source2);
			graph.AddNode(sink);
			graph.Connect(source1.Output, sink.Input);

			Assert.Throws<InvalidOperationException>(() => graph.Connect(source2.Output, sink.Input));
		}

		[Test]
		public void RemoveNode_NotInGraph_ShouldNotDisposeNode()
		{
			var graph = new NodeGraph();
			var node = new InitializableSourceNode();
			node.Initialize();

			graph.RemoveNode(node);

			Assert.That(node.IsInitialized, Is.True);
			node.Dispose();
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
			using (Assert.EnterMultipleScope())
			{
				Assert.That(executionOrder[0], Is.SameAs(source));
				Assert.That(executionOrder[1], Is.SameAs(sink));
			}
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

			using (Assert.EnterMultipleScope())
			{
				// Assert
				Assert.That(sink1.ReceivedValue, Is.EqualTo("shared"));
				Assert.That(sink2.ReceivedValue, Is.EqualTo("shared"));
			}
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
			using (Assert.EnterMultipleScope())
			{
				Assert.That(executionOrder[0], Is.SameAs(source));
				Assert.That(executionOrder[^1], Is.SameAs(sink));
			}
		}

		[Test]
		public void Execute_TriggerableNodeWithDisconnectedTrigger_ShouldExecuteNormally()
		{
			// Arrange
			var graph = new NodeGraph();
			var source = new TriggerableSourceNode { ProducedValue = "frame" };
			var sink = new SinkNode();
			graph.AddNode(source);
			graph.AddNode(sink);
			graph.Connect(source.Output, sink.Input);

			// Act
			graph.Execute();

			// Assert
			using (Assert.EnterMultipleScope())
			{
				Assert.That(source.ExecuteCount, Is.EqualTo(1));
				Assert.That(sink.ExecuteCount, Is.EqualTo(1));
				Assert.That(sink.ReceivedValue, Is.EqualTo("frame"));
			}
		}

		[Test]
		public void Execute_TriggerableNodeWithManualTrigger_ShouldOnlyRunBranchWhenTriggered()
		{
			// Arrange
			var graph = new NodeGraph();
			var trigger = new ManualTriggerNode();
			var source = new TriggerableSourceNode { ProducedValue = "frame" };
			var sink = new SinkNode();
			graph.AddNode(trigger);
			graph.AddNode(source);
			graph.AddNode(sink);
			graph.Connect(trigger.TriggerOutput, source.TriggerInput);
			graph.Connect(source.Output, sink.Input);

			// Act
			graph.Execute();

			// Assert
			using (Assert.EnterMultipleScope())
			{
				Assert.That(source.ExecuteCount, Is.Zero);
				Assert.That(sink.ExecuteCount, Is.Zero);
				Assert.That(sink.ReceivedValue, Is.Null);
			}

			// Act
			trigger.Trigger();
			graph.Execute();

			// Assert
			using (Assert.EnterMultipleScope())
			{
				Assert.That(source.ExecuteCount, Is.EqualTo(1));
				Assert.That(sink.ExecuteCount, Is.EqualTo(1));
				Assert.That(sink.ReceivedValue, Is.EqualTo("frame"));
			}

			// Act
			graph.Execute();

			// Assert
			using (Assert.EnterMultipleScope())
			{
				Assert.That(source.ExecuteCount, Is.EqualTo(1));
				Assert.That(sink.ExecuteCount, Is.EqualTo(1));
			}
		}

		[Test]
		public void Execute_TriggerableNodeWithTimeTrigger_ShouldRunImmediatelyThenWaitForInterval()
		{
			// Arrange
			var graph = new NodeGraph();
			var trigger = new TimeTriggerNode { FramesPerSecond = 1.0 / 60 };
			var source = new TriggerableSourceNode { ProducedValue = "frame" };
			var sink = new SinkNode();
			graph.AddNode(trigger);
			graph.AddNode(source);
			graph.AddNode(sink);
			graph.Connect(trigger.TriggerOutput, source.TriggerInput);
			graph.Connect(source.Output, sink.Input);

			// Act
			graph.Execute();
			graph.Execute();

			// Assert
			using (Assert.EnterMultipleScope())
			{
				Assert.That(source.ExecuteCount, Is.EqualTo(1));
				Assert.That(sink.ExecuteCount, Is.EqualTo(1));
				Assert.That(sink.ReceivedValue, Is.EqualTo("frame"));
			}
		}

		[Test]
		public void TimeTriggerNode_InvalidFramesPerSecond_ShouldKeepLastValidRate()
		{
			// Arrange
			var trigger = new TimeTriggerNode { FramesPerSecond = 2.5 };

			// Act
			trigger.FramesPerSecond = double.NaN;
			trigger.FramesPerSecond = double.PositiveInfinity;

			// Assert
			Assert.That(trigger.FramesPerSecond, Is.EqualTo(2.5));

			// Act
			trigger.FramesPerSecond = -1;

			// Assert
			Assert.That(trigger.FramesPerSecond, Is.Zero);
		}

		[Test]
		public void Initialize_ShouldInitializeInitializableNodesInOrder()
		{
			// Arrange
			var initLog = new List<Node>();
			var initializingNodes = new List<Node>();
			var initializedNodes = new List<Node>();
			var graph = new NodeGraph();
			var source = new InitializableSourceNode { InitLog = initLog };
			var sink = new SinkNode();
			graph.AddNode(sink);
			graph.AddNode(source);
			graph.Connect(source.Output, sink.Input);

			// Act
			graph.Initialize(initializingNodes.Add, initializedNodes.Add);

			// Assert
			Assert.That(initLog, Has.Count.EqualTo(1));
			using (Assert.EnterMultipleScope())
			{
				Assert.That(initLog[0], Is.SameAs(source));
				Assert.That(initializingNodes, Is.EqualTo([source]));
				Assert.That(initializedNodes, Is.EqualTo([source]));
				Assert.That(source.IsInitialized, Is.True);
			}
		}

		[Test]
		public void Initialize_WhenNodeThrows_ShouldPreserveTheFailingNode()
		{
			var graph = new NodeGraph();
			var node = new FailingInitializableNode();
			graph.AddNode(node);

			var exception = Assert.Throws<NodeExecutionException>(() => graph.Initialize());

			using (Assert.EnterMultipleScope())
			{
				Assert.That(exception!.Node, Is.SameAs(node));
				Assert.That(exception.InnerException, Is.TypeOf<InvalidOperationException>());
			}
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

			// Act & Assert â€” should not throw
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

			// Act â€” initialize again
			graph.Initialize();

			// Assert â€” should not have initialized again
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

			// Act & Assert â€” should not throw
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

			using (Assert.EnterMultipleScope())
			{
				// Assert
				Assert.That(initLog, Has.Count.EqualTo(1));
				Assert.That(execLog, Has.Count.EqualTo(2));
				Assert.That(sink.ReceivedValue, Is.EqualTo("initialized"));
				Assert.That(source.IsInitialized, Is.False);
			}
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
		public int ExecuteCount { get; private set; }

		public SinkNode()
		{
			Input = AddInput("Input", typeof(object));
		}

		public override void Execute()
		{
			ExecuteCount++;
			ReceivedValue = Input.Value;
			ExecutionLog?.Add(this);
		}
	}

	internal sealed class TriggerableSourceNode : Node, ITriggerableNode
	{
		public Port TriggerInput { get; }
		public Port Output { get; }
		public object? ProducedValue { get; set; }
		public int ExecuteCount { get; private set; }

		public TriggerableSourceNode()
		{
			TriggerInput = AddInput("Trigger", typeof(TriggerSignal));
			Output = AddOutput("Output", typeof(object));
		}

		public override void Execute()
		{
			ExecuteCount++;
			Output.Value = ProducedValue;
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

	internal sealed class FailingInitializableNode : Node, IInitializable
	{
		public bool IsInitialized => false;

		public void Initialize() => throw new InvalidOperationException("Initialization failed.");

		public override void Execute() { }

		public void Dispose() { }
	}
}
