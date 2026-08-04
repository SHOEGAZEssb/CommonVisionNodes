using System.Text.Json;
using System.Text.Json.Serialization;
using CommonVisionNodes.Contracts;
using CommonVisionNodes.Runtime;
using CommonVisionNodes.Runtime.Definitions;

namespace CommonVisionNodes.Test
{
	public class CodeGeneratorTests
	{
		[Test]
		public void NodePreviewSettings_ShouldHonorDefaultsAndExplicitFlags()
		{
			using (Assert.EnterMultipleScope())
			{
				Assert.That(NodePreviewSettings.IsEnabled("ImageNode", []), Is.False);
				Assert.That(NodePreviewSettings.IsEnabled("GenericVisualizerNode", []), Is.True);
				Assert.That(NodePreviewSettings.IsEnabled("CodeReaderNode", []), Is.True);
				Assert.That(NodePreviewSettings.IsEnabled("ImageNode",
				[
					new NodePropertyDto { Name = NodePreviewSettings.ShowPreviewPropertyName, Value = bool.TrueString }
				]), Is.True);
				Assert.That(NodePreviewSettings.IsEnabled("GenericVisualizerNode",
				[
					new NodePropertyDto { Name = NodePreviewSettings.ShowPreviewPropertyName, Value = bool.FalseString }
				]), Is.False);
			}
		}

		[Test]
		public void RuntimeNodeCatalog_ShouldExposePreviewToggleDefaults()
		{
			var catalog = new RuntimeNodeCatalog();
			var definitions = catalog.GetDefinitions();

			var imageDefinition = definitions.Single(definition => definition.Type == nameof(ImageNode));
			var imagePreviewToggle = imageDefinition.Properties.Single(property => property.Name == NodePreviewSettings.ShowPreviewPropertyName);
			Assert.That(imagePreviewToggle.DefaultValue, Is.EqualTo(bool.FalseString));

			var visualizerDefinition = definitions.Single(definition => definition.Type == nameof(GenericVisualizerNode));
			var visualizerPreviewToggle = visualizerDefinition.Properties.Single(property => property.Name == NodePreviewSettings.ShowPreviewPropertyName);
			Assert.That(visualizerPreviewToggle.DefaultValue, Is.EqualTo(bool.TrueString));

			var histogramDefinition = definitions.Single(definition => definition.Type == nameof(HistogramNode));
			Assert.That(histogramDefinition.Properties.Any(property => property.Name == NodePreviewSettings.ShowPreviewPropertyName), Is.False);

			var codeReaderDefinition = definitions.Single(definition => definition.Type == nameof(CodeReaderNode));
			using (Assert.EnterMultipleScope())
			{
				Assert.That(codeReaderDefinition.PreviewKind, Is.EqualTo(NodePreviewKindDto.CodeReader));
				Assert.That(codeReaderDefinition.InputPorts.Single().Type, Is.EqualTo("Image"));
				Assert.That(codeReaderDefinition.OutputPorts.Single(port => port.Name == "Data").Type, Is.EqualTo("String"));
				Assert.That(codeReaderDefinition.Properties.Single(property => property.Name == NodePreviewSettings.ShowPreviewPropertyName).DefaultValue, Is.EqualTo(bool.TrueString));
			}

			var gevServerDefinition = definitions.Single(definition => definition.Type == nameof(GevServerNode));
			using (Assert.EnterMultipleScope())
			{
				Assert.That(gevServerDefinition.InputPorts.Single().Type, Is.EqualTo("Image"));
				Assert.That(gevServerDefinition.Properties.Any(property => property.Name == NodePreviewSettings.ShowPreviewPropertyName), Is.True);
			}

			var adapterProperty = gevServerDefinition.Properties.Single(property => property.Name == nameof(GevServerNode.LocalAddress));
			using (Assert.EnterMultipleScope())
			{
				Assert.That(adapterProperty.ValueKind, Is.EqualTo(NodePropertyValueKindDto.Enum));
				Assert.That(adapterProperty.Options, Is.Not.Empty);
			}
		}

		[Test]
		public void RuntimeNodeCatalog_Definitions_ShouldSerializeForApi()
		{
			var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
			{
				DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
			};
			options.Converters.Add(new JsonStringEnumConverter());

			var definitions = new RuntimeNodeCatalog().GetDefinitions();

			Assert.DoesNotThrow(() => JsonSerializer.Serialize(definitions, options));
		}

		[Test]
		public void Generate_ImageNodeOnly_ShouldContainImageFromFile()
		{
			var graph = new NodeGraph();
			var node = new ImageNode { FilePath = @"C:\test.bmp" };
			graph.AddNode(node);

			var code = CodeGenerator.Generate(graph);

			Assert.That(code, Does.Contain("using Stemmer.Cvb;"));
			Assert.That(code, Does.Contain("Image.FromFile(@\"C:\\test.bmp\")"));
			Assert.That(code, Does.Not.Contain("using System.Runtime.InteropServices;"));
			Assert.That(code, Does.Not.Contain("using Stemmer.Cvb.Driver;"));
		}

		[Test]
		public void Generate_ImageToSave_ShouldWireVariables()
		{
			var graph = new NodeGraph();
			var imageNode = new ImageNode { FilePath = @"C:\input.bmp" };
			var saveNode = new SaveImageNode { FilePath = @"C:\output.bmp" };
			graph.AddNode(imageNode);
			graph.AddNode(saveNode);
			graph.Connect(imageNode.ImageOutput, saveNode.ImageInput);

			var code = CodeGenerator.Generate(graph);

			Assert.That(code, Does.Contain("using var sourceImage = Image.FromFile(@\"C:\\input.bmp\")"));
			Assert.That(code, Does.Contain("sourceImage.Save(@\"C:\\output.bmp\")"));
		}

		[Test]
		public void Generate_ImageToGevServer_ShouldEmitServerStreamingCode()
		{
			var graph = new NodeGraph();
			var imageNode = new ImageNode { FilePath = @"C:\input.bmp" };
			var gevServerNode = new GevServerNode
			{
				LocalAddress = "192.168.1.10",
				ResendBuffersCount = 2
			};
			graph.AddNode(imageNode);
			graph.AddNode(gevServerNode);
			graph.Connect(imageNode.ImageOutput, gevServerNode.ImageInput);

			var code = CodeGenerator.Generate(graph);

			Assert.That(code, Does.Contain("using Stemmer.Cvb.GevServer;"));
			Assert.That(code, Does.Contain("using System.Net;"));
			Assert.That(code, Does.Contain("GevServer.CreateWithConstSize(sourceImage.Size, sourceImage.ColorModel, sourceImage.Planes[0].DataType, DriverType.Socket)"));
			Assert.That(code, Does.Contain("gevServer.Stream.ResendBuffersCount = 2;"));
			Assert.That(code, Does.Contain("gevServer.Start(IPAddress.Parse(@\"192.168.1.10\"));"));
			Assert.That(code, Does.Contain("gevStream.TrySend(sourceImage)"));
		}

		[Test]
		public void Generate_ImageToBinarizeToSave_ShouldEmitHelperMethod()
		{
			var graph = new NodeGraph();
			var imageNode = new ImageNode { FilePath = @"C:\input.bmp" };
			var binarizeNode = new BinarizeNode { Threshold = 100 };
			var saveNode = new SaveImageNode { FilePath = @"C:\output.bmp" };
			graph.AddNode(imageNode);
			graph.AddNode(binarizeNode);
			graph.AddNode(saveNode);
			graph.Connect(imageNode.ImageOutput, binarizeNode.ImageInput);
			graph.Connect(binarizeNode.ImageOutput, saveNode.ImageInput);

			var code = CodeGenerator.Generate(graph);

			Assert.That(code, Does.Contain("using System.Runtime.InteropServices;"));
			Assert.That(code, Does.Contain("using var binarized = Binarize(sourceImage, 100)"));
			Assert.That(code, Does.Contain("binarized.Save(@\"C:\\output.bmp\")"));
			Assert.That(code, Does.Contain("static Image Binarize(Image source, int threshold)"));
		}

		[Test]
		public void Generate_ImageToSubImage_ShouldEmitCropHelper()
		{
			var graph = new NodeGraph();
			var imageNode = new ImageNode { FilePath = @"C:\input.bmp" };
			var subImageNode = new SubImageNode { AreaX = 10, AreaY = 20, AreaWidth = 100, AreaHeight = 50 };
			graph.AddNode(imageNode);
			graph.AddNode(subImageNode);
			graph.Connect(imageNode.ImageOutput, subImageNode.ImageInput);

			var code = CodeGenerator.Generate(graph);

			Assert.That(code, Does.Contain("using var cropped = Crop(sourceImage, 10, 20, 100, 50)"));
			Assert.That(code, Does.Contain("static Image Crop(Image source, int areaX, int areaY, int areaWidth, int areaHeight)"));
		}

		[Test]
		public void Generate_ImageToMatrixTransform_ShouldEmitAffineAndBilinearHelpers()
		{
			var graph = new NodeGraph();
			var imageNode = new ImageNode { FilePath = @"C:\input.bmp" };
			var transformNode = new MatrixTransformNode { Angle = 45.0, ScaleX = 2.0, ScaleY = 2.0 };
			graph.AddNode(imageNode);
			graph.AddNode(transformNode);
			graph.Connect(imageNode.ImageOutput, transformNode.ImageInput);

			var code = CodeGenerator.Generate(graph);

			Assert.That(code, Does.Contain("AffineTransform(sourceImage, 45.0, 2.0, 2.0, 0.0, 0.0)"));
			Assert.That(code, Does.Contain("static Image AffineTransform("));
			Assert.That(code, Does.Contain("static byte SampleBilinear("));
		}

		[Test]
		public void Generate_DeviceNode_ShouldEmitDeviceAcquisitionCode()
		{
			var graph = new NodeGraph();
			var deviceNode = new DeviceNode { AccessToken = @"C:\path\to\driver.vin" };
			var saveNode = new SaveImageNode { FilePath = @"C:\output.bmp" };
			graph.AddNode(deviceNode);
			graph.AddNode(saveNode);
			graph.Connect(deviceNode.ImageOutput, saveNode.ImageInput);

			var code = CodeGenerator.Generate(graph);

			Assert.That(code, Does.Contain("using Stemmer.Cvb.Driver;"));
			Assert.That(code, Does.Contain("using System.Linq;"));
			Assert.That(code, Does.Contain("DeviceFactory.Discover(DiscoverFlags.IgnoreVins)"));
			Assert.That(code, Does.Contain("DiscoveryProperties.DeviceSerialNumber"));
			Assert.That(code, Does.Contain("DeviceFactory.Open("));
			Assert.That(code, Does.Contain("GetStream<ImageStream>(0)"));
			Assert.That(code, Does.Contain(".Start()"));
			Assert.That(code, Does.Contain(".WaitFor(TimeSpan.FromSeconds(3))"));
			Assert.That(code, Does.Contain(".Clone()"));
			Assert.That(code, Does.Contain(".TryStop()"));
			Assert.That(code, Does.Contain("acquiredImage.Save(@\"C:\\output.bmp\")"));
		}

		[Test]
		public void RuntimeNodePropertyBinder_CodeReader_ShouldApplyEnumAndNumericProperties()
		{
			var node = new CodeReaderNode();

			RuntimeNodePropertyBinder.Apply(node,
			[
				new NodePropertyDto { Name = nameof(CodeReaderNode.Symbologies), Value = nameof(CodeReaderSymbologySelection.TwoDimensional) },
				new NodePropertyDto { Name = "CodePolarity", Value = "DarkOnLight" },
				new NodePropertyDto { Name = nameof(CodeReaderNode.DetectorDensity), Value = "9" },
				new NodePropertyDto { Name = nameof(CodeReaderNode.MaxCodes), Value = "5" },
				new NodePropertyDto { Name = nameof(CodeReaderNode.TimeLimitMs), Value = "250" },
				new NodePropertyDto { Name = nameof(CodeReaderNode.BasicInkjetDpmEnabled), Value = bool.TrueString }
			]);

			using (Assert.EnterMultipleScope())
			{
				Assert.That(node.Symbologies, Is.EqualTo(CodeReaderSymbologySelection.TwoDimensional));
				Assert.That(node.GetType().GetProperty("CodePolarity")?.GetValue(node)?.ToString(), Is.EqualTo("DarkOnLight"));
				Assert.That(node.DetectorDensity, Is.EqualTo(4));
				Assert.That(node.MaxCodes, Is.EqualTo(5));
				Assert.That(node.TimeLimitMs, Is.EqualTo(250));
				Assert.That(node.BasicInkjetDpmEnabled, Is.True);
			}
		}

		[Test]
		public void Generate_ImageToCodeReader_ShouldEmitCodeReaderConfiguration()
		{
			var graph = new NodeGraph();
			var imageNode = new ImageNode { FilePath = @"C:\input.bmp" };
			var codeReaderNode = new CodeReaderNode
			{
				Symbologies = CodeReaderSymbologySelection.TwoDimensional,
				MaxCodes = 5,
				TimeLimitMs = 250
			};
			RuntimeNodePropertyBinder.Apply(codeReaderNode,
			[
				new NodePropertyDto { Name = "CodePolarity", Value = "DarkOnLight" }
			]);
			graph.AddNode(imageNode);
			graph.AddNode(codeReaderNode);
			graph.Connect(imageNode.ImageOutput, codeReaderNode.ImageInput);

			var code = CodeGenerator.Generate(graph);

			Assert.That(code, Does.Contain("using Stemmer.Cvb.CodeReader;"));
			Assert.That(code, Does.Contain("using Stemmer.Cvb.CodeReader.Config;"));
			Assert.That(code, Does.Contain("using var decoder = Decoder.Create();"));
			Assert.That(code, Does.Contain("decoder.GetConfig<DataMatrix>().SetEnabled(true).SetPolarity(Polarity.DarkOnLight);"));
			Assert.That(code, Does.Contain("decoder.GetConfig<QR>().SetEnabled(true).SetPolarity(Polarity.DarkOnLight);"));
			Assert.That(code, Does.Contain("decoder.GetConfig<Pdf417>().SetEnabled(true);"));
			Assert.That(code, Does.Contain("decoder.ExecuteFor(sourceImage.Planes[0], TimeSpan.FromMilliseconds(250), 5)"));
			Assert.That(code, Does.Contain("decodedData"));
		}

		[Test]
		public void Generate_MultipleImageNodes_ShouldCreateUniqueVariableNames()
		{
			var graph = new NodeGraph();
			var imageNode1 = new ImageNode { FilePath = @"C:\a.bmp" };
			var imageNode2 = new ImageNode { FilePath = @"C:\b.bmp" };
			graph.AddNode(imageNode1);
			graph.AddNode(imageNode2);

			var code = CodeGenerator.Generate(graph);

			Assert.That(code, Does.Contain("using var sourceImage = Image.FromFile(@\"C:\\a.bmp\")"));
			Assert.That(code, Does.Contain("using var sourceImage2 = Image.FromFile(@\"C:\\b.bmp\")"));
		}

		[Test]
		public void Generate_MultipleCSharpNodes_ShouldEmitDistinctHelperMethods()
		{
			var graph = new NodeGraph();
			var imageNode = new ImageNode { FilePath = @"C:\input.bmp" };
			var firstScript = new CSharpNode { Code = "return inputImage;" };
			var secondScript = new CSharpNode { Code = "return Filter.Gauss(inputImage, FixedFilterSize.Kernel3x3);" };
			graph.AddNode(imageNode);
			graph.AddNode(firstScript);
			graph.AddNode(secondScript);
			graph.Connect(imageNode.ImageOutput, firstScript.ImageInput);
			graph.Connect(firstScript.ImageOutput, secondScript.ImageInput);

			var code = CodeGenerator.Generate(graph);

			using (Assert.EnterMultipleScope())
			{
				Assert.That(code, Does.Contain("var csharp = ProcessCustomCode(sourceImage);"));
				Assert.That(code, Does.Contain("var csharp2 = ProcessCustomCode2(csharp);"));
				Assert.That(code, Does.Contain("static Image ProcessCustomCode(Image inputImage)"));
				Assert.That(code, Does.Contain("static Image ProcessCustomCode2(Image inputImage)"));
				Assert.That(code, Does.Contain("return inputImage;"));
				Assert.That(code, Does.Contain("return Filter.Gauss(inputImage, FixedFilterSize.Kernel3x3);"));
			}
		}

		[Test]
		public void Generate_EmptyGraph_ShouldReturnMinimalUsings()
		{
			var graph = new NodeGraph();

			var code = CodeGenerator.Generate(graph);

			Assert.That(code, Does.Contain("using Stemmer.Cvb;"));
			Assert.That(code, Does.Not.Contain("using System.Runtime.InteropServices;"));
			Assert.That(code, Does.Not.Contain("Helper Methods"));
		}

		[Test]
		public void Generate_ChainedProcessing_ShouldWireAllVariablesCorrectly()
		{
			var graph = new NodeGraph();
			var imageNode = new ImageNode { FilePath = @"C:\input.bmp" };
			var binarizeNode = new BinarizeNode { Threshold = 128 };
			var subImageNode = new SubImageNode { AreaX = 0, AreaY = 0, AreaWidth = 64, AreaHeight = 64 };
			var saveNode = new SaveImageNode { FilePath = @"C:\output.bmp" };
			graph.AddNode(imageNode);
			graph.AddNode(binarizeNode);
			graph.AddNode(subImageNode);
			graph.AddNode(saveNode);
			graph.Connect(imageNode.ImageOutput, binarizeNode.ImageInput);
			graph.Connect(binarizeNode.ImageOutput, subImageNode.ImageInput);
			graph.Connect(subImageNode.ImageOutput, saveNode.ImageInput);

			var code = CodeGenerator.Generate(graph);

			Assert.That(code, Does.Contain("using var binarized = Binarize(sourceImage, 128)"));
			Assert.That(code, Does.Contain("using var cropped = Crop(binarized, 0, 0, 64, 64)"));
			Assert.That(code, Does.Contain("cropped.Save(@\"C:\\output.bmp\")"));
		}

		[Test]
		public void Generate_UnconnectedSaveNode_ShouldNotEmitSaveCode()
		{
			var graph = new NodeGraph();
			var saveNode = new SaveImageNode { FilePath = @"C:\output.bmp" };
			graph.AddNode(saveNode);

			var code = CodeGenerator.Generate(graph);

			Assert.That(code, Does.Not.Contain(".Save("));
		}
	}
}
