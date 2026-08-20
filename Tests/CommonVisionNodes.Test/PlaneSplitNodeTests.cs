using System.Runtime.InteropServices;
using CommonVisionNodes.Contracts;
using CommonVisionNodes.Runtime;
using CommonVisionNodes.Runtime.Definitions;
using CommonVisionNodes.Runtime.Execution;
using Stemmer.Cvb;

namespace CommonVisionNodes.Test;

public sealed class PlaneSplitNodeTests
{
	[Test]
	public void Constructor_ShouldCreateOneImageOutputPerConfiguredPlane()
	{
		var node = new PlaneSplitNode();

		using (Assert.EnterMultipleScope())
		{
			Assert.That(node.ImageInput.Name, Is.EqualTo("Image"));
			Assert.That(node.PlaneCount, Is.EqualTo(3));
			Assert.That(node.Mode, Is.EqualTo(PlaneSplitMode.Copy));
			Assert.That(node.PlaneOutputs.Select(port => port.Name), Is.EqualTo(["Plane 0", "Plane 1", "Plane 2"]));
			Assert.That(node.PlaneOutputs, Has.All.Property(nameof(Port.Type)).EqualTo(typeof(Image)));
		}
	}

	[Test]
	public void Execute_WithLinkMode_ShouldShareTheSourcePlanePixels()
	{
		using var source = new Image(new Size2D(2, 1), 3);
		WriteByte(source, 1, 0, 20);
		var node = new PlaneSplitNode { PlaneCount = 3, Mode = PlaneSplitMode.Link };
		node.ImageInput.Value = source;
		node.Execute();

		var linkedPlane = (Image)node.PlaneOutputs[1].Value!;
		try
		{
			Assert.That(linkedPlane, Is.InstanceOf<MappedImage>());
			WriteByte(source, 1, 0, 99);
			Assert.That(ReadByte(linkedPlane, 0), Is.EqualTo(99));
		}
		finally
		{
			linkedPlane.Dispose();
		}
	}

	[Test]
	public void PlaneCount_ShouldResizeOutputPortsAndClampItsValue()
	{
		var node = new PlaneSplitNode { PlaneCount = 2 };
		Assert.That(node.PlaneOutputs.Select(port => port.Name), Is.EqualTo(["Plane 0", "Plane 1"]));

		node.PlaneCount = 99;

		using (Assert.EnterMultipleScope())
		{
			Assert.That(node.PlaneCount, Is.EqualTo(PlaneSplitNode.MaximumPlaneCount));
			Assert.That(node.PlaneOutputs, Has.Count.EqualTo(PlaneSplitNode.MaximumPlaneCount));
		}
	}

	[Test]
	public void Execute_ShouldCopyEachPlaneIntoItsCorrespondingSinglePlaneOutput()
	{
		using var source = new Image(new Size2D(2, 1), 3);
		WriteByte(source, 0, 0, 10);
		WriteByte(source, 1, 0, 20);
		WriteByte(source, 2, 0, 30);
		WriteByte(source, 0, 1, 40);
		WriteByte(source, 1, 1, 50);
		WriteByte(source, 2, 1, 60);

		var node = new PlaneSplitNode { PlaneCount = 3 };
		node.ImageInput.Value = source;
		node.Execute();

		var outputs = node.PlaneOutputs.Select(port => (Image)port.Value!).ToArray();
		try
		{
			using (Assert.EnterMultipleScope())
			{
				Assert.That(outputs, Has.All.Property(nameof(Image.Planes)).Count.EqualTo(1));
				Assert.That(ReadByte(outputs[0], 0), Is.EqualTo(10));
				Assert.That(ReadByte(outputs[1], 0), Is.EqualTo(20));
				Assert.That(ReadByte(outputs[2], 0), Is.EqualTo(30));
				Assert.That(ReadByte(outputs[0], 1), Is.EqualTo(40));
				Assert.That(ReadByte(outputs[1], 1), Is.EqualTo(50));
				Assert.That(ReadByte(outputs[2], 1), Is.EqualTo(60));
			}
		}
		finally
		{
			foreach (var output in outputs)
				output.Dispose();
		}
	}

	[Test]
	public void Execute_WithDifferentInputPlaneCount_ShouldExplainTheMismatch()
	{
		using var source = new Image(new Size2D(1, 1), 2);
		var node = new PlaneSplitNode { PlaneCount = 3 };
		node.ImageInput.Value = source;

		var exception = Assert.Throws<InvalidOperationException>(node.Execute);
		Assert.That(exception!.Message, Does.Contain("configured for 3 plane(s), but the input image has 2"));
	}

	[Test]
	public void RuntimeGraphFactory_ShouldApplyPlaneCountBeforeResolvingPlaneOutputs()
	{
		var graphDto = new GraphDto
		{
			Nodes =
			[
				new NodeDto
				{
					Id = "split",
					Type = nameof(PlaneSplitNode),
					Properties =
					[
						new NodePropertyDto { Name = nameof(PlaneSplitNode.PlaneCount), Value = "2" },
						new NodePropertyDto { Name = nameof(PlaneSplitNode.Mode), Value = nameof(PlaneSplitMode.Link) }
					]
				},
				new NodeDto { Id = "save", Type = nameof(SaveImageNode) }
			],
			Connections =
			[
				new ConnectionDto
				{
					OutputNodeId = "split",
					OutputPortName = "Plane 1",
					InputNodeId = "save",
					InputPortName = "Image"
				}
			]
		};

		using var result = new RuntimeGraphFactory(new RuntimeNodeCatalog()).Build(graphDto);
		var split = (PlaneSplitNode)result.NodesById["split"];

		using (Assert.EnterMultipleScope())
		{
			Assert.That(split.PlaneCount, Is.EqualTo(2));
			Assert.That(split.Mode, Is.EqualTo(PlaneSplitMode.Link));
			Assert.That(split.PlaneOutputs, Has.Count.EqualTo(2));
			Assert.That(result.Graph.Connections.Single().Output.Name, Is.EqualTo("Plane 1"));
		}
	}

	[Test]
	public void CodeGenerator_WithLinkMode_ShouldCreateMappedPlaneImages()
	{
		var graph = new NodeGraph();
		var source = new ImageNode { FilePath = @"C:\input.bmp" };
		var split = new PlaneSplitNode { PlaneCount = 2, Mode = PlaneSplitMode.Link };
		var save = new SaveImageNode { FilePath = @"C:\plane-one.bmp" };
		graph.AddNode(source);
		graph.AddNode(split);
		graph.AddNode(save);
		graph.Connect(source.ImageOutput, split.ImageInput);
		graph.Connect(split.PlaneOutputs[1], save.ImageInput);

		var code = CodeGenerator.Generate(graph);

		using (Assert.EnterMultipleScope())
		{
			Assert.That(code, Does.Contain("using var splitPlane0 = Image.FromPlanes(MappingOption.LinkPixels, sourceImage.Planes[0]);"));
			Assert.That(code, Does.Contain("using var splitPlane1 = Image.FromPlanes(MappingOption.LinkPixels, sourceImage.Planes[1]);"));
			Assert.That(code, Does.Not.Contain("static Image SplitPlane(Image source, int planeIndex)"));
		}
	}

	[Test]
	public void CodeGenerator_ShouldRegisterEachPlaneOutputSeparately()
	{
		var graph = new NodeGraph();
		var source = new ImageNode { FilePath = @"C:\input.bmp" };
		var split = new PlaneSplitNode { PlaneCount = 2 };
		var save = new SaveImageNode { FilePath = @"C:\plane-one.bmp" };
		graph.AddNode(source);
		graph.AddNode(split);
		graph.AddNode(save);
		graph.Connect(source.ImageOutput, split.ImageInput);
		graph.Connect(split.PlaneOutputs[1], save.ImageInput);

		var code = CodeGenerator.Generate(graph);

		using (Assert.EnterMultipleScope())
		{
			Assert.That(code, Does.Contain("using var splitPlane0 = SplitPlane(sourceImage, 0);"));
			Assert.That(code, Does.Contain("using var splitPlane1 = SplitPlane(sourceImage, 1);"));
			Assert.That(code, Does.Contain("static Image SplitPlane(Image source, int planeIndex)"));
			Assert.That(code, Does.Contain("splitPlane1.Save(@\"C:\\plane-one.bmp\")"));
		}
	}

	private static void WriteByte(Image image, int planeIndex, int x, byte value)
	{
		var access = image.Planes[planeIndex].GetLinearAccess();
		var pixel = access.BasePtr + checked((nint)(x * access.XInc));
		Marshal.WriteByte(pixel, value);
	}

	private static byte ReadByte(Image image, int x)
	{
		var access = image.Planes[0].GetLinearAccess();
		var pixel = access.BasePtr + checked((nint)(x * access.XInc));
		return Marshal.ReadByte(pixel);
	}
}
