namespace CommonVisionNodes.Test
{
    public class CodeGeneratorTests
    {
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
            Assert.That(code, Does.Contain("DeviceFactory.Open("));
            Assert.That(code, Does.Contain("GetStream<ImageStream>(0)"));
            Assert.That(code, Does.Contain(".Start()"));
            Assert.That(code, Does.Contain(".WaitFor(TimeSpan.FromSeconds(3))"));
            Assert.That(code, Does.Contain(".Clone()"));
            Assert.That(code, Does.Contain(".TryStop()"));
            Assert.That(code, Does.Contain("acquiredImage.Save(@\"C:\\output.bmp\")"));
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
