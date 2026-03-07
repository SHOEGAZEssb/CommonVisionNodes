using Stemmer.Cvb;

namespace CommonVisionNodes.Test
{
    public class ImageNodeTests
    {
        [Test]
        public void Constructor_ShouldCreateOutputPort()
        {
            // Arrange & Act
            var node = new ImageNode();

            // Assert
            Assert.That(node.Outputs, Has.Count.EqualTo(1));
            Assert.That(node.ImageOutput.Name, Is.EqualTo("Image"));
            Assert.That(node.ImageOutput.Type, Is.EqualTo(typeof(Image)));
            Assert.That(node.ImageOutput.Direction, Is.EqualTo(PortDirection.Output));
        }

        [Test]
        public void Constructor_ShouldHaveNoInputPorts()
        {
            // Arrange & Act
            var node = new ImageNode();

            // Assert
            Assert.That(node.Inputs, Is.Empty);
        }

        [Test]
        public void Constructor_ShouldImplementIInitializable()
        {
            // Arrange & Act
            var node = new ImageNode();

            // Assert
            Assert.That(node, Is.InstanceOf<IInitializable>());
        }

        [Test]
        public void Initialize_ShouldSetIsInitializedTrue()
        {
            // Arrange
            var tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.bmp");
            using var testImage = new Image(64, 64);
            testImage.Save(tempFile);

            var node = new ImageNode { FilePath = tempFile };

            try
            {
                // Act
                node.Initialize();

                // Assert
                Assert.That(node.IsInitialized, Is.True);
            }
            finally
            {
                node.Dispose();
                File.Delete(tempFile);
            }
        }

        [Test]
        public void Execute_AfterInitialize_ShouldSetOutputValue()
        {
            // Arrange
            var tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.bmp");
            using var testImage = new Image(64, 64);
            testImage.Save(tempFile);

            var node = new ImageNode { FilePath = tempFile };

            try
            {
                node.Initialize();

                // Act
                node.Execute();

                // Assert
                Assert.That(node.ImageOutput.Value, Is.Not.Null);
                Assert.That(node.ImageOutput.Value, Is.InstanceOf<Image>());
            }
            finally
            {
                node.Dispose();
                File.Delete(tempFile);
            }
        }

        [Test]
        public void Execute_WithoutInitialize_ShouldThrow()
        {
            // Arrange
            var node = new ImageNode { FilePath = "dummy.bmp" };

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => node.Execute());
        }

        [Test]
        public void Execute_MultipleTimes_ShouldReturnSameInstance()
        {
            // Arrange
            var tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.bmp");
            using var testImage = new Image(64, 64);
            testImage.Save(tempFile);

            var node = new ImageNode { FilePath = tempFile };

            try
            {
                node.Initialize();

                // Act
                node.Execute();
                var first = node.ImageOutput.Value;
                node.Execute();
                var second = node.ImageOutput.Value;

                // Assert
                Assert.That(second, Is.SameAs(first));
            }
            finally
            {
                node.Dispose();
                File.Delete(tempFile);
            }
        }

        [Test]
        public void Dispose_ShouldResetIsInitialized()
        {
            // Arrange
            var tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.bmp");
            using var testImage = new Image(64, 64);
            testImage.Save(tempFile);

            var node = new ImageNode { FilePath = tempFile };
            node.Initialize();

            // Act
            node.Dispose();

            // Assert
            Assert.That(node.IsInitialized, Is.False);
            File.Delete(tempFile);
        }

        [Test]
        public void Reinitialize_WithDifferentFile_ShouldLoadNewImage()
        {
            // Arrange
            var tempFile1 = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.bmp");
            var tempFile2 = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.bmp");
            using var testImage1 = new Image(64, 64);
            using var testImage2 = new Image(32, 32);
            testImage1.Save(tempFile1);
            testImage2.Save(tempFile2);

            var node = new ImageNode { FilePath = tempFile1 };

            try
            {
                node.Initialize();
                node.Execute();
                var first = node.ImageOutput.Value;

                node.FilePath = tempFile2;
                node.Initialize();
                node.Execute();
                var second = node.ImageOutput.Value;

                // Assert
                Assert.That(second, Is.Not.SameAs(first));
            }
            finally
            {
                node.Dispose();
                File.Delete(tempFile1);
                File.Delete(tempFile2);
            }
        }
    }

    public class SaveImageNodeTests
    {
        [Test]
        public void Constructor_ShouldCreateInputPort()
        {
            // Arrange & Act
            var node = new SaveImageNode();

            // Assert
            Assert.That(node.Inputs, Has.Count.EqualTo(1));
            Assert.That(node.ImageInput.Name, Is.EqualTo("Image"));
            Assert.That(node.ImageInput.Type, Is.EqualTo(typeof(Image)));
            Assert.That(node.ImageInput.Direction, Is.EqualTo(PortDirection.Input));
        }

        [Test]
        public void Constructor_ShouldHaveNoOutputPorts()
        {
            // Arrange & Act
            var node = new SaveImageNode();

            // Assert
            Assert.That(node.Outputs, Is.Empty);
        }

        [Test]
        public void Constructor_ShouldNotImplementIInitializable()
        {
            // Arrange & Act
            var node = new SaveImageNode();

            // Assert
            Assert.That(node, Is.Not.InstanceOf<IInitializable>());
        }

        [Test]
        public void Execute_ShouldSaveImageToFile()
        {
            // Arrange
            var tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.bmp");
            using var testImage = new Image(64, 64);

            var node = new SaveImageNode { FilePath = tempFile };
            node.ImageInput.Value = testImage;

            try
            {
                // Act
                node.Execute();

                // Assert
                Assert.That(File.Exists(tempFile), Is.True);
            }
            finally
            {
                File.Delete(tempFile);
            }
        }
    }

    public class ImagePipelineTests
    {
        [Test]
        public void FullLifecycle_InitializeExecuteDispose_ShouldWork()
        {
            // Arrange
            var sourcePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.bmp");
            var destPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.bmp");

            using var testImage = new Image(64, 64);
            testImage.Save(sourcePath);

            using var graph = new NodeGraph();
            var imageNode = new ImageNode { FilePath = sourcePath };
            var saveNode = new SaveImageNode { FilePath = destPath };
            graph.AddNode(imageNode);
            graph.AddNode(saveNode);
            graph.Connect(imageNode.ImageOutput, saveNode.ImageInput);

            try
            {
                // Act
                graph.Initialize();
                graph.Execute();

                // Assert
                Assert.That(imageNode.IsInitialized, Is.True);
                Assert.That(File.Exists(destPath), Is.True);
                using var loadedImage = Image.FromFile(destPath);
                Assert.That(loadedImage, Is.Not.Null);
            }
            finally
            {
                File.Delete(sourcePath);
                File.Delete(destPath);
            }
        }

        [Test]
        public void Dispose_ShouldDisposeInitializableNodes()
        {
            // Arrange
            var tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.bmp");
            using var testImage = new Image(64, 64);
            testImage.Save(tempFile);

            var graph = new NodeGraph();
            var imageNode = new ImageNode { FilePath = tempFile };
            graph.AddNode(imageNode);

            graph.Initialize();
            Assert.That(imageNode.IsInitialized, Is.True);

            // Act
            graph.Dispose();

            // Assert
            Assert.That(imageNode.IsInitialized, Is.False);
            File.Delete(tempFile);
        }

        [Test]
        public void Execute_MultipleRuns_ShouldReuseInitializedResources()
        {
            // Arrange
            var sourcePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.bmp");
            var destPath1 = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.bmp");
            var destPath2 = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.bmp");

            using var testImage = new Image(64, 64);
            testImage.Save(sourcePath);

            using var graph = new NodeGraph();
            var imageNode = new ImageNode { FilePath = sourcePath };
            var saveNode = new SaveImageNode { FilePath = destPath1 };
            graph.AddNode(imageNode);
            graph.AddNode(saveNode);
            graph.Connect(imageNode.ImageOutput, saveNode.ImageInput);

            try
            {
                graph.Initialize();

                // Act — first run
                graph.Execute();
                Assert.That(File.Exists(destPath1), Is.True);

                // Act — second run with different output path
                saveNode.FilePath = destPath2;
                graph.Execute();
                Assert.That(File.Exists(destPath2), Is.True);
            }
            finally
            {
                File.Delete(sourcePath);
                File.Delete(destPath1);
                File.Delete(destPath2);
            }
        }
    }
}
