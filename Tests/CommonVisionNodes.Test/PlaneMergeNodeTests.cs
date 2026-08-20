using System.Reflection;
using System.Runtime.InteropServices;
using CommonVisionNodes.Contracts;
using CommonVisionNodes.Runtime;
using CommonVisionNodes.Runtime.Definitions;
using CommonVisionNodes.Runtime.Execution;
using Stemmer.Cvb;

namespace CommonVisionNodes.Test;

public sealed class PlaneMergeNodeTests
{
	[Test]
	public void Constructor_ShouldCreateOneImageInputPerConfiguredPlane()
	{
		var node = new PlaneMergeNode();

		using (Assert.EnterMultipleScope())
		{
			Assert.That(node.PlaneCount, Is.EqualTo(3));
			Assert.That(node.PlaneInputs.Select(port => port.Name), Is.EqualTo(["Plane 0", "Plane 1", "Plane 2"]));
			Assert.That(node.PlaneInputs, Has.All.Property(nameof(Port.Type)).EqualTo(typeof(Image)));
			Assert.That(node.PlaneWeights, Is.EqualTo("1,1,1"));
			Assert.That(node.ImageOutput.Name, Is.EqualTo("Image"));
		}
	}

	[Test]
	public void Execute_ShouldApplyEachPlaneWeightAndSaturateTheOutput()
	{
		using var first = CreatePlane(100, 200);
		using var second = CreatePlane(200, 100);
		var node = new PlaneMergeNode { PlaneCount = 2, PlaneWeights = "0.5, 1.5" };
		node.PlaneInputs[0].Value = first;
		node.PlaneInputs[1].Value = second;

		node.Execute();

		var result = (Image)node.ImageOutput.Value!;
		try
		{
			using (Assert.EnterMultipleScope())
			{
				Assert.That(ReadByte(result, 0, 0), Is.EqualTo(50));
				Assert.That(ReadByte(result, 0, 1), Is.EqualTo(100));
				Assert.That(ReadByte(result, 1, 0), Is.EqualTo(255));
				Assert.That(ReadByte(result, 1, 1), Is.EqualTo(150));
			}
		}
		finally
		{
			result.Dispose();
		}
	}

	[Test]
	public void PlaneCount_ShouldResizeInputPortsAndClampItsValue()
	{
		var node = new PlaneMergeNode { PlaneCount = 2 };
		Assert.That(node.PlaneInputs.Select(port => port.Name), Is.EqualTo(["Plane 0", "Plane 1"]));

		node.PlaneCount = 99;

		using (Assert.EnterMultipleScope())
		{
			Assert.That(node.PlaneCount, Is.EqualTo(PlaneMergeNode.MaximumPlaneCount));
			Assert.That(node.PlaneInputs, Has.Count.EqualTo(PlaneMergeNode.MaximumPlaneCount));
		}
	}

	[Test]
	public void Execute_ShouldMergeSinglePlaneInputsInPortOrder()
	{
		using var red = CreatePlane(10, 40);
		using var green = CreatePlane(20, 50);
		using var blue = CreatePlane(30, 60);
		var node = new PlaneMergeNode { PlaneCount = 3 };
		node.PlaneInputs[0].Value = red;
		node.PlaneInputs[1].Value = green;
		node.PlaneInputs[2].Value = blue;

		node.Execute();

		var result = (Image)node.ImageOutput.Value!;
		try
		{
			using (Assert.EnterMultipleScope())
			{
				Assert.That(result.Planes, Has.Count.EqualTo(3));
				Assert.That(ReadByte(result, 0, 0), Is.EqualTo(10));
				Assert.That(ReadByte(result, 1, 0), Is.EqualTo(20));
				Assert.That(ReadByte(result, 2, 0), Is.EqualTo(30));
				Assert.That(ReadByte(result, 0, 1), Is.EqualTo(40));
				Assert.That(ReadByte(result, 1, 1), Is.EqualTo(50));
				Assert.That(ReadByte(result, 2, 1), Is.EqualTo(60));
			}
		}
		finally
		{
			result.Dispose();
		}
	}

	[Test]
	public void Execute_WithMultiPlaneInput_ShouldExplainTheCompatibilityFailure()
	{
		using var multiPlane = new Image(new Size2D(1, 1), 2);
		using var singlePlane = new Image(new Size2D(1, 1), 1);
		var node = new PlaneMergeNode { PlaneCount = 2 };
		node.PlaneInputs[0].Value = multiPlane;
		node.PlaneInputs[1].Value = singlePlane;

		var exception = Assert.Throws<InvalidOperationException>(node.Execute);
		Assert.That(exception!.Message, Does.Contain("must have exactly one plane"));
	}

	[Test]
	public void RuntimeGraphFactory_ShouldApplyPlaneCountBeforeResolvingPlaneInputs()
	{
		var graphDto = new GraphDto
		{
			Nodes =
			[
				new NodeDto { Id = "generator", Type = nameof(ImageGeneratorNode) },
				new NodeDto
				{
					Id = "merge",
					Type = nameof(PlaneMergeNode),
					Properties =
					[
						new NodePropertyDto { Name = nameof(PlaneMergeNode.PlaneCount), Value = "2" },
						new NodePropertyDto { Name = nameof(PlaneMergeNode.PlaneWeights), Value = "0.5,1.5" }
					]
				}
			],
			Connections =
			[
				new ConnectionDto
				{
					OutputNodeId = "generator",
					OutputPortName = "Image",
					InputNodeId = "merge",
					InputPortName = "Plane 1"
				}
			]
		};

		using var result = new RuntimeGraphFactory(new RuntimeNodeCatalog()).Build(graphDto);
		var merge = (PlaneMergeNode)result.NodesById["merge"];

		using (Assert.EnterMultipleScope())
		{
			Assert.That(merge.PlaneCount, Is.EqualTo(2));
			Assert.That(merge.PlaneInputs, Has.Count.EqualTo(2));
			Assert.That(merge.Weights, Is.EqualTo([0.5, 1.5]));
			Assert.That(result.Graph.Connections.Single().Input.Name, Is.EqualTo("Plane 1"));
		}
	}

	[Test]
	public void LivePropertyUpdate_ShouldApplyWeightsWithoutChangingPlaneCount()
	{
		var node = new PlaneMergeNode();
		var properties = new[]
		{
			new NodePropertyDto { Name = nameof(PlaneMergeNode.PlaneCount), Value = "2" },
			new NodePropertyDto { Name = nameof(PlaneMergeNode.PlaneWeights), Value = "0.5,1.5" }
		};
		var getLiveProperties = typeof(GraphExecutionRunner).GetMethod("GetLiveProperties", BindingFlags.NonPublic | BindingFlags.Static)!;
		var liveProperties = ((IEnumerable<NodePropertyDto>)getLiveProperties.Invoke(null, [node, properties])!).ToList();

		RuntimeNodePropertyBinder.Apply(node, liveProperties);

		using (Assert.EnterMultipleScope())
		{
			Assert.That(liveProperties.Select(property => property.Name), Is.EqualTo([nameof(PlaneMergeNode.PlaneWeights)]));
			Assert.That(node.PlaneCount, Is.EqualTo(3));
			Assert.That(node.Weights, Is.EqualTo([0.5, 1.5, 1.0]));
		}
	}

	[Test]
	public void CodeGenerator_ShouldRegisterTheMergedImageOutput()
	{
		var graph = new NodeGraph();
		var source = new ImageNode { FilePath = @"C:\input.bmp" };
		var generated = new ImageGeneratorNode();
		var merge = new PlaneMergeNode { PlaneCount = 2 };
		var save = new SaveImageNode { FilePath = @"C:\merged.bmp" };
		graph.AddNode(source);
		graph.AddNode(generated);
		graph.AddNode(merge);
		graph.AddNode(save);
		graph.Connect(source.ImageOutput, merge.PlaneInputs[0]);
		graph.Connect(generated.ImageOutput, merge.PlaneInputs[1]);
		graph.Connect(merge.ImageOutput, save.ImageInput);

		var code = CodeGenerator.Generate(graph);

		using (Assert.EnterMultipleScope())
		{
			Assert.That(code, Does.Contain("using var mergedImage = MergePlanes([sourceImage, generatedImage], [1.0, 1.0]);"));
			Assert.That(code, Does.Contain("static Image MergePlanes(Image[] sources, double[] weights)"));
			Assert.That(code, Does.Contain("mergedImage.Save(@\"C:\\merged.bmp\")"));
		}
	}

	private static Image CreatePlane(byte firstValue, byte secondValue)
	{
		var image = new Image(new Size2D(2, 1), 1);
		WriteByte(image, 0, 0, firstValue);
		WriteByte(image, 0, 1, secondValue);
		return image;
	}

	private static void WriteByte(Image image, int planeIndex, int x, byte value)
	{
		var access = image.Planes[planeIndex].GetLinearAccess();
		var pixel = access.BasePtr + checked((nint)(x * access.XInc));
		Marshal.WriteByte(pixel, value);
	}

	private static byte ReadByte(Image image, int planeIndex, int x)
	{
		var access = image.Planes[planeIndex].GetLinearAccess();
		var pixel = access.BasePtr + checked((nint)(x * access.XInc));
		return Marshal.ReadByte(pixel);
	}
}
