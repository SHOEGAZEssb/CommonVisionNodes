using System.Reflection;
using CommonVisionNodes.Contracts;
using CommonVisionNodes.Runtime;
using CommonVisionNodes.Runtime.Execution;

namespace CommonVisionNodes.Test;

public sealed class RuntimeGraphFactoryTests
{
    [Test]
    public void Build_ShouldCreateNodesApplyPropertiesAndConnections()
    {
        var factory = new RuntimeGraphFactory(new RuntimeNodeCatalog());
        var graphDto = new GraphDto
        {
            Nodes =
            [
                new NodeDto
                {
                    Id = "generator",
                    Type = nameof(ImageGeneratorNode),
                    Properties =
                    [
                        new NodePropertyDto { Name = nameof(ImageGeneratorNode.Width), Value = "32" },
                        new NodePropertyDto { Name = nameof(ImageGeneratorNode.Height), Value = "16" },
                        new NodePropertyDto { Name = nameof(ImageGeneratorNode.Pattern), Value = nameof(TestPattern.Rings) },
                        new NodePropertyDto { Name = nameof(ImageGeneratorNode.Speed), Value = "5" }
                    ]
                },
                new NodeDto
                {
                    Id = "visualizer",
                    Type = nameof(GenericVisualizerNode)
                }
            ],
            Connections =
            [
                new ConnectionDto
                {
                    OutputNodeId = "generator",
                    OutputPortName = "Image",
                    InputNodeId = "visualizer",
                    InputPortName = "Data"
                }
            ]
        };

        using var result = factory.Build(graphDto);

        Assert.That(result.NodesById.Keys, Is.EquivalentTo(new[] { "generator", "visualizer" }));
        Assert.That(result.Graph.Connections, Has.Count.EqualTo(1));
        Assert.That(result.NodesById["generator"], Is.TypeOf<ImageGeneratorNode>());
        Assert.That(result.NodesById["visualizer"], Is.TypeOf<GenericVisualizerNode>());

        var generator = (ImageGeneratorNode)result.NodesById["generator"];
        Assert.Multiple(() =>
        {
            Assert.That(generator.Width, Is.EqualTo(32));
            Assert.That(generator.Height, Is.EqualTo(16));
            Assert.That(generator.Pattern, Is.EqualTo(TestPattern.Rings));
            Assert.That(generator.Speed, Is.EqualTo(5));
        });
    }

    [Test]
    public void Build_WithMissingNodeId_ShouldThrow()
    {
        var factory = new RuntimeGraphFactory(new RuntimeNodeCatalog());
        var graphDto = new GraphDto
        {
            Nodes = [new NodeDto { Id = "", Type = nameof(ImageGeneratorNode) }]
        };

        Assert.Throws<InvalidOperationException>(() => factory.Build(graphDto));
    }

    [Test]
    public void Build_WithUnknownNodeType_ShouldThrow()
    {
        var factory = new RuntimeGraphFactory(new RuntimeNodeCatalog());
        var graphDto = new GraphDto
        {
            Nodes = [new NodeDto { Id = "missing", Type = "MissingNode" }]
        };

        Assert.Throws<InvalidOperationException>(() => factory.Build(graphDto));
    }

    [Test]
    public void Build_WithUnknownConnectionEndpoint_ShouldThrow()
    {
        var factory = new RuntimeGraphFactory(new RuntimeNodeCatalog());
        var graphDto = new GraphDto
        {
            Nodes = [new NodeDto { Id = "generator", Type = nameof(ImageGeneratorNode) }],
            Connections =
            [
                new ConnectionDto
                {
                    OutputNodeId = "generator",
                    OutputPortName = "Image",
                    InputNodeId = "missing",
                    InputPortName = "Data"
                }
            ]
        };

        Assert.Throws<InvalidOperationException>(() => factory.Build(graphDto));
    }

    [Test]
    public void Build_WithIncompatiblePorts_ShouldThrow()
    {
        var factory = new RuntimeGraphFactory(new RuntimeNodeCatalog());
        var graphDto = new GraphDto
        {
            Nodes =
            [
                new NodeDto { Id = "generator", Type = nameof(ImageGeneratorNode) },
                new NodeDto { Id = "classifier", Type = nameof(PolimagoClassifyNode) }
            ],
            Connections =
            [
                new ConnectionDto
                {
                    OutputNodeId = "generator",
                    OutputPortName = "Image",
                    InputNodeId = "classifier",
                    InputPortName = "Blobs"
                }
            ]
        };

        Assert.Throws<InvalidOperationException>(() => factory.Build(graphDto));
    }
}

public sealed class GraphExecutionRunnerTests
{
    [Test]
    public async Task SingleExecution_ShouldPublishStateNodePreviewAndCompletionMessages()
    {
        var messages = new List<ExecutionMessageDto>();
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var request = CreateSingleGeneratorRequest(showPreview: true);
        var runner = CreateRunner(request, messages, completed);

        runner.Start();
        await completed.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await runner.DisposeAsync();

        Assert.That(messages.Any(message => message.MessageType == ExecutionMessageTypeDto.Failure), Is.False);
        Assert.That(messages.Select(message => message.ExecutionState?.Status).Where(status => status.HasValue),
            Does.Contain(ExecutionStatusDto.Starting)
                .And.Contain(ExecutionStatusDto.Initializing)
                .And.Contain(ExecutionStatusDto.Running)
                .And.Contain(ExecutionStatusDto.Completed));
        Assert.That(messages.Any(message =>
            message.MessageType == ExecutionMessageTypeDto.NodeUpdate &&
            message.NodeUpdate?.NodeId == "generator" &&
            message.NodeUpdate.Status == NodeExecutionStatusDto.Succeeded), Is.True);

        var imagePreview = messages
            .Where(message => message.MessageType == ExecutionMessageTypeDto.ImagePreview)
            .Select(message => message.ImagePreview)
            .SingleOrDefault(preview => preview?.NodeId == "generator");

        Assert.That(imagePreview, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(imagePreview!.Encoding, Is.EqualTo(ImagePreviewEncodingDto.Bgra32));
            Assert.That(imagePreview.MediaType, Is.EqualTo("application/x-bgra32"));
            Assert.That(imagePreview.Base64Data.Length, Is.GreaterThan(0));
            Assert.That(imagePreview.Stride, Is.EqualTo(imagePreview.PreviewWidth * 4));
        });
    }

    [Test]
    public async Task SingleExecution_WithInvalidGraph_ShouldPublishFailureMessage()
    {
        var messages = new List<ExecutionMessageDto>();
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var request = new ExecutionRequestDto
        {
            ClientId = "test-client",
            Mode = ExecutionModeDto.Single,
            Graph = new GraphDto
            {
                Nodes = [new NodeDto { Id = "missing", Type = "MissingNode" }]
            }
        };
        var runner = CreateRunner(request, messages, completed);

        runner.Start();
        await completed.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await runner.DisposeAsync();

        var failure = messages.LastOrDefault(message => message.MessageType == ExecutionMessageTypeDto.Failure);
        Assert.That(failure, Is.Not.Null);
        Assert.That(failure!.ExecutionState?.Status, Is.EqualTo(ExecutionStatusDto.Failed));
        Assert.That(failure.ExecutionState?.Message, Does.Contain("Unknown node type"));
    }

    [TestCase(0, 1000.0)]
    [TestCase(1, 1000.0)]
    [TestCase(30, 1000.0 / 30)]
    [TestCase(60, 1000.0 / 60)]
    [TestCase(999, 1000.0 / 999)]
    [TestCase(1000, 1.0)]
    [TestCase(1001, 0)]
    [TestCase(2000, 0)]
    public void PreviewRefreshRate_ShouldMapToExpectedInterval(int refreshRate, double expectedIntervalMs)
    {
        var method = typeof(GraphExecutionRunner).GetMethod(
            "GetPreviewIntervalMilliseconds",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.That(method, Is.Not.Null);
        var interval = (double)method!.Invoke(null, [refreshRate])!;
        Assert.That(interval, Is.EqualTo(expectedIntervalMs).Within(0.0001));
    }

    [TestCase(0, 32, 16)]
    [TestCase(64, 32, 16)]
    [TestCase(16, 16, 8)]
    [TestCase(10, 10, 5)]
    public async Task SingleExecution_ShouldHonorPreviewMaxDimension(int maxDimension, int expectedPreviewWidth, int expectedPreviewHeight)
    {
        var messages = new List<ExecutionMessageDto>();
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var request = CreateSingleGeneratorRequest(showPreview: true, previewImageMaxDimension: maxDimension);
        var runner = CreateRunner(request, messages, completed);

        runner.Start();
        await completed.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await runner.DisposeAsync();

        var imagePreview = messages
            .Where(message => message.MessageType == ExecutionMessageTypeDto.ImagePreview)
            .Select(message => message.ImagePreview)
            .SingleOrDefault(preview => preview?.NodeId == "generator");

        Assert.That(imagePreview, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(imagePreview!.Width, Is.EqualTo(32));
            Assert.That(imagePreview.Height, Is.EqualTo(16));
            Assert.That(imagePreview.PreviewWidth, Is.EqualTo(expectedPreviewWidth));
            Assert.That(imagePreview.PreviewHeight, Is.EqualTo(expectedPreviewHeight));
            Assert.That(imagePreview.Stride, Is.EqualTo(expectedPreviewWidth * 4));
        });
    }

    private static GraphExecutionRunner CreateRunner(
        ExecutionRequestDto request,
        List<ExecutionMessageDto> messages,
        TaskCompletionSource completed)
    {
        var graphFactory = new RuntimeGraphFactory(new RuntimeNodeCatalog());
        var previewFactory = new RuntimePreviewFactory();

        return new GraphExecutionRunner(
            request,
            graphFactory,
            previewFactory,
            (message, _) =>
            {
                lock (messages)
                    messages.Add(message);

                if (message.MessageType is ExecutionMessageTypeDto.Completed or ExecutionMessageTypeDto.Failure)
                    completed.TrySetResult();

                return Task.CompletedTask;
            },
            _ => completed.TrySetResult());
    }

    private static ExecutionRequestDto CreateSingleGeneratorRequest(bool showPreview, int previewImageMaxDimension = 64)
        => new()
        {
            ClientId = "test-client",
            Mode = ExecutionModeDto.Single,
            PreviewRefreshRate = 30,
            PreviewImageMaxDimension = previewImageMaxDimension,
            Graph = new GraphDto
            {
                Nodes =
                [
                    new NodeDto
                    {
                        Id = "generator",
                        Type = nameof(ImageGeneratorNode),
                        Properties =
                        [
                            new NodePropertyDto { Name = nameof(ImageGeneratorNode.Width), Value = "32" },
                            new NodePropertyDto { Name = nameof(ImageGeneratorNode.Height), Value = "16" },
                            new NodePropertyDto { Name = nameof(ImageGeneratorNode.Pattern), Value = nameof(TestPattern.GradientH) },
                            new NodePropertyDto { Name = nameof(ImageGeneratorNode.Speed), Value = "1" },
                            new NodePropertyDto { Name = NodePreviewSettings.ShowPreviewPropertyName, Value = showPreview.ToString() }
                        ]
                    }
                ]
            }
        };
}
