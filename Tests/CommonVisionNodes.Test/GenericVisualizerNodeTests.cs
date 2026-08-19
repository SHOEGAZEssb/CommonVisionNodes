using CommonVisionNodes.Contracts;
using CommonVisionNodes.Runtime;
using CommonVisionNodes.Runtime.Definitions;
using CommonVisionNodes.Runtime.Execution;
using Stemmer.Cvb;

namespace CommonVisionNodes.Test;

public sealed class GenericVisualizerNodeTests
{
	[Test]
	public void Execute_ShouldVisualizeAndPassThroughTheReceivedValue()
	{
		var node = new GenericVisualizerNode();
		var value = new object();
		node.DataInput.Value = value;

		node.Execute();

		using (Assert.EnterMultipleScope())
		{
			Assert.That(node.DataOutput.Name, Is.EqualTo("Data"));
			Assert.That(node.DataOutput.Type, Is.EqualTo(typeof(object)));
			Assert.That(node.DataOutput.Direction, Is.EqualTo(PortDirection.Output));
			Assert.That(node.LastValue, Is.SameAs(value));
			Assert.That(node.DataOutput.Value, Is.SameAs(value));
		}
	}

	[Test]
	public void Execute_InGraph_ShouldPassValueToDownstreamNode()
	{
		var graph = new NodeGraph();
		var source = new SourceNode { ProducedValue = "visualized" };
		var visualizer = new GenericVisualizerNode();
		var sink = new SinkNode();
		graph.AddNode(source);
		graph.AddNode(visualizer);
		graph.AddNode(sink);
		graph.Connect(source.Output, visualizer.DataInput);
		graph.Connect(visualizer.DataOutput, sink.Input);

		graph.Execute();

		Assert.That(sink.ReceivedValue, Is.EqualTo("visualized"));
	}

	[Test]
	public void Connect_ThroughVisualizer_ShouldUseTheUpstreamImageType()
	{
		var graph = new NodeGraph();
		var source = new ImageSourceNode();
		var visualizer = new GenericVisualizerNode();
		var sink = new ImageSinkNode();
		graph.AddNode(source);
		graph.AddNode(visualizer);
		graph.AddNode(sink);
		graph.Connect(source.ImageOutput, visualizer.DataInput);

		Assert.DoesNotThrow(() => graph.Connect(visualizer.DataOutput, sink.ImageInput));
	}

	[Test]
	public void Connect_ThroughVisualizer_ShouldRejectAnIncompatibleUpstreamType()
	{
		var graph = new NodeGraph();
		var source = new StringSourceNode();
		var visualizer = new GenericVisualizerNode();
		var sink = new ImageSinkNode();
		graph.AddNode(source);
		graph.AddNode(visualizer);
		graph.AddNode(sink);
		graph.Connect(source.Output, visualizer.DataInput);

		Assert.Throws<InvalidOperationException>(() => graph.Connect(visualizer.DataOutput, sink.ImageInput));
	}

	[Test]
	public void Build_WithVisualizerConnectionsOutOfOrder_ShouldResolveThePassThroughType()
	{
		var factory = new RuntimeGraphFactory(new RuntimeNodeCatalog());
		var graphDto = new GraphDto
		{
			Nodes =
			[
				new NodeDto { Id = "source", Type = nameof(ImageGeneratorNode) },
				new NodeDto { Id = "visualizer", Type = nameof(GenericVisualizerNode) },
				new NodeDto { Id = "sink", Type = nameof(SaveImageNode) }
			],
			Connections =
			[
				new ConnectionDto
				{
					OutputNodeId = "visualizer",
					OutputPortName = "Data",
					InputNodeId = "sink",
					InputPortName = "Image"
				},
				new ConnectionDto
				{
					OutputNodeId = "source",
					OutputPortName = "Image",
					InputNodeId = "visualizer",
					InputPortName = "Data"
				}
			]
		};

		using var result = factory.Build(graphDto);

		Assert.That(result.Graph.Connections, Has.Count.EqualTo(2));
	}

	private sealed class ImageSourceNode : Node
	{
		public Port ImageOutput { get; }

		public ImageSourceNode() => ImageOutput = AddOutput("Image", typeof(Image));

		public override void Execute() { }
	}

	private sealed class StringSourceNode : Node
	{
		public Port Output { get; }

		public StringSourceNode() => Output = AddOutput("Output", typeof(string));

		public override void Execute() { }
	}

	private sealed class ImageSinkNode : Node
	{
		public Port ImageInput { get; }

		public ImageSinkNode() => ImageInput = AddInput("Image", typeof(Image));

		public override void Execute() { }
	}
}
