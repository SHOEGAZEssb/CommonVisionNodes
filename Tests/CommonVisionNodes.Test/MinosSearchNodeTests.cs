using CommonVisionNodes.Contracts;
using CommonVisionNodes.Runtime;
using CommonVisionNodes.Runtime.Definitions;
using CommonVisionNodes.Runtime.Execution;
using Stemmer.Cvb;

namespace CommonVisionNodes.Test;

public sealed class MinosSearchNodeTests
{
    [Test]
    public void Catalog_ShouldExposeMinosSearchDefinitionAndDefaults()
    {
        var definition = new RuntimeNodeCatalog().GetDefinition(nameof(MinosSearchNode));

        Assert.That(definition, Is.Not.Null);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(definition!.DisplayName, Is.EqualTo("Minos Search"));
            Assert.That(definition.PreviewKind, Is.EqualTo(NodePreviewKindDto.Classification));
            Assert.That(definition.InputPorts.Single().Type, Is.EqualTo("Image"));
            Assert.That(definition.OutputPorts.Single(port => port.Name == "Results").Type,
                Is.EqualTo("IReadOnlyList<MinosSearchResultItem>"));
            Assert.That(definition.Properties.Single(property => property.Name == nameof(MinosSearchNode.SearchOperation)).DefaultValue,
                Is.EqualTo(nameof(MinosSearchOperation.FindAll)));
            Assert.That(definition.Properties.Single(property => property.Name == nameof(MinosSearchNode.Density)).DefaultValue,
                Is.EqualTo("1"));
            Assert.That(definition.Properties.Single(property => property.Name == nameof(MinosSearchNode.Locality)).DefaultValue,
                Is.EqualTo("10"));
            Assert.That(definition.Properties.Single(property => property.Name == nameof(MinosSearchNode.MaxResults)).DefaultValue,
                Is.EqualTo("100"));
        }
    }

    [Test]
    public void GenerateCode_ShouldUseSelectedMinosOperationAndParameters()
    {
        var graph = new NodeGraph();
        var image = new ImageNode { FilePath = @"C:\images\input.bmp" };
        var minos = new MinosSearchNode
        {
            ClassifierPath = @"C:\models\parts.clf",
            SearchOperation = MinosSearchOperation.FindBestSubPixel,
            Density = 0.75,
            MinQuality = 0.8,
            Locality = 12,
            MaxResults = 5
        };
        graph.AddNode(image);
        graph.AddNode(minos);
        graph.Connect(image.ImageOutput, minos.ImageInput);

        var code = CodeGenerator.Generate(graph);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(code, Does.Contain("using Stemmer.Cvb.Minos;"));
            Assert.That(code, Does.Contain("new Classifier(@\"C:\\models\\parts.clf\")"));
            Assert.That(code, Does.Contain("QualityFeedback.Normalized"));
            Assert.That(code, Does.Contain("SearchMode.FindBestSubPixel"));
            Assert.That(code, Does.Contain(", 0.75)"));
            Assert.That(code, Does.Contain(".Threshold = 0.8"));
            Assert.That(code, Does.Contain(".Count >= 5"));
        }
    }

    [Test]
    public void Execute_WithInstalledClaraTutorial_ShouldFindNoseAndPassImageThrough()
    {
        var cvbPath = Environment.GetEnvironmentVariable("CVB");
        if (string.IsNullOrWhiteSpace(cvbPath))
        {
            Assert.Ignore("CVB is not installed or the CVB environment variable is not set.");
            return;
        }

        var classifierPath = Path.Combine(cvbPath, "Tutorial", "Minos", "Images", "Clara", "Clara.clf");
        var imagePath = Path.Combine(cvbPath, "Tutorial", "Minos", "Images", "Clara", "Clara1.bmp");
        if (!File.Exists(classifierPath) || !File.Exists(imagePath))
        {
            Assert.Ignore("The installed CVB Minos Clara tutorial data is unavailable.");
            return;
        }

        using var image = Image.FromFile(imagePath);
        var node = new MinosSearchNode
        {
            ClassifierPath = classifierPath,
            SearchOperation = MinosSearchOperation.FindAll,
            Density = 1.0,
            MinQuality = 0.5,
            Locality = 10,
            MaxResults = 10
        };

        try
        {
            node.ImageInput.Value = image;
            node.Initialize();
            node.Execute();

            Assert.That(node.Results, Has.Count.EqualTo(1));
            using (Assert.EnterMultipleScope())
            {
                Assert.That(node.Results[0].ClassName, Is.EqualTo("Nose"));
                Assert.That(node.Results[0].Quality, Is.EqualTo(1.0).Within(0.001));
                Assert.That(node.Results[0].X, Is.EqualTo(94).Within(0.5));
                Assert.That(node.Results[0].Y, Is.EqualTo(137).Within(0.5));
                Assert.That(node.ImageOutput.Value, Is.SameAs(image));
                Assert.That(node.ResultsOutput.Value, Is.SameAs(node.Results));
            }

            var preview = RuntimePreviewFactory.CreatePreviewMessage("minos", node, previewImageMaxDimension: 0);
            Assert.That(preview?.ClassificationPreview?.Results, Has.Count.EqualTo(1));
            Assert.That(preview?.ClassificationPreview?.Results[0].ClassName, Is.EqualTo("Nose"));
        }
        finally
        {
            node.Dispose();
        }
    }
}
