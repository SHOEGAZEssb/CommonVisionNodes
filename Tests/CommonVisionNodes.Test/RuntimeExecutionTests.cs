using System.Diagnostics;
using System.Reflection;
using CommonVisionNodes.Contracts;
using CommonVisionNodes.Runtime;
using CommonVisionNodes.Runtime.Definitions;
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

		using (Assert.EnterMultipleScope())
		{
			Assert.That(result.NodesById.Keys, Is.EquivalentTo(["generator", "visualizer"]));
			Assert.That(result.Graph.Connections, Has.Count.EqualTo(1));
			Assert.That(result.NodesById["generator"], Is.TypeOf<ImageGeneratorNode>());
			Assert.That(result.NodesById["visualizer"], Is.TypeOf<GenericVisualizerNode>());
		}

		var generator = (ImageGeneratorNode)result.NodesById["generator"];
		using (Assert.EnterMultipleScope())
		{
			Assert.That(generator.Width, Is.EqualTo(32));
			Assert.That(generator.Height, Is.EqualTo(16));
			Assert.That(generator.Pattern, Is.EqualTo(TestPattern.Rings));
			Assert.That(generator.Speed, Is.EqualTo(5));
		}
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

	[Test]
	public void Build_TimeTriggerWithNonFiniteFramesPerSecond_ShouldKeepDefaultRate()
	{
		var factory = new RuntimeGraphFactory(new RuntimeNodeCatalog());
		var graphDto = new GraphDto
		{
			Nodes =
			[
				new NodeDto
				{
					Id = "trigger",
					Type = nameof(TimeTriggerNode),
					Properties =
					[
						new NodePropertyDto { Name = nameof(TimeTriggerNode.FramesPerSecond), Value = "NaN" }
					]
				}
			]
		};

		using var result = factory.Build(graphDto);

		var trigger = (TimeTriggerNode)result.NodesById["trigger"];
		Assert.That(trigger.FramesPerSecond, Is.EqualTo(1.0));
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

		using (Assert.EnterMultipleScope())
		{
			Assert.That(messages.Select(message => message.ExecutionId), Is.All.EqualTo(runner.ExecutionId));
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
		}

		var imagePreview = messages
			.Where(message => message.MessageType == ExecutionMessageTypeDto.ImagePreview)
			.Select(message => message.ImagePreview)
			.SingleOrDefault(preview => preview?.NodeId == "generator");

		Assert.That(imagePreview, Is.Not.Null);
		using (Assert.EnterMultipleScope())
		{
			Assert.That(imagePreview!.Encoding, Is.EqualTo(ImagePreviewEncodingDto.Gray8));
			Assert.That(imagePreview.PreviewSequence, Is.GreaterThan(0));
			Assert.That(imagePreview.MediaType, Is.EqualTo("application/x-gray8"));
			Assert.That(imagePreview.BinaryData, Is.Not.Null.And.Not.Empty);
			Assert.That(imagePreview.Stride, Is.EqualTo(imagePreview.PreviewWidth));
		}
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
		using (Assert.EnterMultipleScope())
		{
			Assert.That(failure!.ExecutionState?.Status, Is.EqualTo(ExecutionStatusDto.Failed));
			Assert.That(failure.ExecutionState?.Message, Does.Contain("Unknown node type"));
		}
	}

	[Test]
	public async Task SingleExecution_WithNodeException_ShouldPublishFailedNodeAndFailureContext()
	{
		var messages = new List<ExecutionMessageDto>();
		var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var request = new ExecutionRequestDto
		{
			ClientId = "test-client",
			Mode = ExecutionModeDto.Single,
			Graph = new GraphDto
			{
				Nodes =
				[
					new NodeDto
					{
						Id = "save",
						Type = nameof(SaveImageNode),
						Properties =
						[
							new NodePropertyDto { Name = nameof(SaveImageNode.FilePath), Value = "not-used.bmp" }
						]
					}
				]
			}
		};
		var runner = CreateRunner(request, messages, completed);

		runner.Start();
		await completed.Task.WaitAsync(TimeSpan.FromSeconds(10));
		await runner.DisposeAsync();

		var nodeFailure = messages.FirstOrDefault(message =>
			message.MessageType == ExecutionMessageTypeDto.NodeUpdate &&
			message.NodeUpdate?.NodeId == "save" &&
			message.NodeUpdate.Status == NodeExecutionStatusDto.Failed);
		var failure = messages.LastOrDefault(message => message.MessageType == ExecutionMessageTypeDto.Failure);

		using (Assert.EnterMultipleScope())
		{
			Assert.That(nodeFailure, Is.Not.Null);
			Assert.That(failure, Is.Not.Null);
		}
		using (Assert.EnterMultipleScope())
		{
			Assert.That(nodeFailure!.NodeUpdate?.Message, Is.Not.Null.And.Not.Empty);
			Assert.That(failure!.ExecutionState?.Status, Is.EqualTo(ExecutionStatusDto.Failed));
			Assert.That(failure.ExecutionState?.Message, Does.Contain(nameof(SaveImageNode)).And.Contain("'save'"));
			Assert.That(failure.Error, Is.EqualTo(failure.ExecutionState?.Message));
		}
	}

	[Test]
	public async Task ContinuousExecution_ShouldUpdateTimeTriggerPropertiesWithoutRestartingGraph()
	{
		var messages = new List<ExecutionMessageDto>();
		var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var request = new ExecutionRequestDto
		{
			ClientId = "test-client",
			Mode = ExecutionModeDto.Continuous,
			PreviewRefreshRate = 30,
			PreviewImageMaxDimension = 64,
			Graph = new GraphDto
			{
				Nodes =
				[
					new NodeDto
					{
						Id = "trigger",
						Type = nameof(TimeTriggerNode),
						Properties =
						[
							new NodePropertyDto { Name = nameof(TimeTriggerNode.FramesPerSecond), Value = "1" }
						]
					}
				]
			}
		};
		var runner = CreateRunner(request, messages, completed);

		runner.Start();

		var updated = false;
		for (var attempt = 0; attempt < 100 && !updated; attempt++)
		{
			updated = runner.UpdateNodeProperties(
				"trigger",
				[new NodePropertyDto { Name = nameof(TimeTriggerNode.FramesPerSecond), Value = "4" }]);

			if (!updated)
				await Task.Delay(20);
		}

		await runner.DisposeAsync();

		using (Assert.EnterMultipleScope())
		{
			Assert.That(updated, Is.True);
			Assert.That(messages.Any(message => message.ExecutionState?.Status == ExecutionStatusDto.Failed), Is.False);
		}
	}

	[Test]
	public async Task ContinuousExecution_WithTimeTrigger_ShouldCountOnlyTriggeredFrames()
	{
		var messages = new List<ExecutionMessageDto>();
		var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var request = CreateSingleGeneratorRequest(showPreview: false);
		request.Mode = ExecutionModeDto.Continuous;
		request.Graph.Nodes.Insert(0, new NodeDto
		{
			Id = "trigger",
			Type = nameof(TimeTriggerNode),
			Properties =
			[
				new NodePropertyDto { Name = nameof(TimeTriggerNode.FramesPerSecond), Value = "2" }
			]
		});
		request.Graph.Connections.Add(new ConnectionDto
		{
			OutputNodeId = "trigger",
			OutputPortName = "Trigger",
			InputNodeId = "generator",
			InputPortName = "Trigger"
		});
		var runner = CreateRunner(request, messages, completed);

		runner.Start();
		await Task.Delay(350);
		await runner.DisposeAsync();

		List<ExecutionMessageDto> snapshot;
		lock (messages)
			snapshot = [.. messages];

		var framesProcessed = snapshot
			.Where(message => message.ExecutionState is not null)
			.Max(message => message.ExecutionState!.FramesProcessed);

		using (Assert.EnterMultipleScope())
		{
			Assert.That(framesProcessed, Is.EqualTo(1));
			Assert.That(snapshot.Any(message => message.MessageType == ExecutionMessageTypeDto.Failure), Is.False);
		}
	}

	[Test]
	public async Task ContinuousExecution_ShouldCountFramesOnlyWhenEveryTerminalNodeRuns()
	{
		var messages = new List<ExecutionMessageDto>();
		var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var request = CreateTerminalCompletionRequest();
		var runner = CreateRunner(request, messages, completed);

		runner.Start();
		await Task.Delay(350);
		await runner.DisposeAsync();

		List<ExecutionMessageDto> snapshot;
		lock (messages)
			snapshot = [.. messages];

		var framesProcessed = snapshot
			.Where(message => message.ExecutionState is not null)
			.Max(message => message.ExecutionState!.FramesProcessed);

		// The untriggered terminal runs continuously, but the triggered terminal runs only once
		// during this interval. A graph frame is complete only when both terminals run.
		Assert.That(framesProcessed, Is.EqualTo(1));
	}

	[Test]
	public async Task ContinuousExecution_ShouldReportIndividualFramesPerSecondForTerminalNodes()
	{
		var messages = new List<ExecutionMessageDto>();
		var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var runner = CreateRunner(CreateTerminalCompletionRequest(), messages, completed);

		runner.Start();
		await Task.Delay(1200);
		await runner.DisposeAsync();

		List<ExecutionMessageDto> snapshot;
		lock (messages)
			snapshot = [.. messages];

		var triggeredTerminalFps = snapshot
			.Where(message => message.NodeUpdate is { NodeId: "triggered-terminal", FramesPerSecond: not null })
			.Select(message => message.NodeUpdate!.FramesPerSecond!.Value)
			.Last();
		var continuousTerminalFps = snapshot
			.Where(message => message.NodeUpdate is { NodeId: "continuous-terminal", FramesPerSecond: not null })
			.Select(message => message.NodeUpdate!.FramesPerSecond!.Value)
			.Last();

		using (Assert.EnterMultipleScope())
		{
			Assert.That(triggeredTerminalFps, Is.GreaterThan(0));
			Assert.That(continuousTerminalFps, Is.GreaterThan(triggeredTerminalFps));
		}
	}

	[Test]
	public async Task ContinuousExecution_ShouldUpdateImageGeneratorSpeedWithoutRestartingGraph()
	{
		var messages = new List<ExecutionMessageDto>();
		var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var request = CreateSingleGeneratorRequest(showPreview: false);
		request.Mode = ExecutionModeDto.Continuous;
		var runner = CreateRunner(request, messages, completed);

		runner.Start();

		var updated = false;
		for (var attempt = 0; attempt < 100 && !updated; attempt++)
		{
			updated = runner.UpdateNodeProperties(
				"generator",
				[
					new NodePropertyDto { Name = nameof(ImageGeneratorNode.Width), Value = "32" },
					new NodePropertyDto { Name = nameof(ImageGeneratorNode.Height), Value = "16" },
					new NodePropertyDto { Name = nameof(ImageGeneratorNode.Pattern), Value = nameof(TestPattern.GradientH) },
					new NodePropertyDto { Name = nameof(ImageGeneratorNode.Speed), Value = "8" },
					new NodePropertyDto { Name = NodePreviewSettings.ShowPreviewPropertyName, Value = bool.FalseString }
				]);

			if (!updated)
				await Task.Delay(20);
		}

		await runner.DisposeAsync();

		using (Assert.EnterMultipleScope())
		{
			Assert.That(updated, Is.True);
			Assert.That(messages.Any(message => message.ExecutionState?.Status == ExecutionStatusDto.Failed), Is.False);
			Assert.That(messages.Select(message => message.ExecutionId), Is.All.EqualTo(runner.ExecutionId));
		}
	}

	[Test]
	public async Task ContinuousExecution_ShouldToggleNodePreviewWithoutRestartingGraph()
	{
		var messages = new List<ExecutionMessageDto>();
		var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var request = CreateSingleGeneratorRequest(showPreview: false);
		request.Mode = ExecutionModeDto.Continuous;
		request.PreviewRefreshRate = 30;
		var runner = CreateRunner(request, messages, completed);

		runner.Start();

		var updated = false;
		for (var attempt = 0; attempt < 100 && !updated; attempt++)
		{
			updated = runner.UpdateNodeProperties(
				"generator",
				[new NodePropertyDto { Name = NodePreviewSettings.ShowPreviewPropertyName, Value = bool.TrueString }]);

			if (!updated)
				await Task.Delay(20);
		}

		var previewReceived = false;
		for (var attempt = 0; attempt < 100 && !previewReceived; attempt++)
		{
			lock (messages)
			{
				previewReceived = messages.Any(message =>
					message.MessageType == ExecutionMessageTypeDto.ImagePreview &&
					message.ImagePreview?.NodeId == "generator");
			}

			if (!previewReceived)
				await Task.Delay(20);
		}

		await runner.DisposeAsync();

		using (Assert.EnterMultipleScope())
		{
			Assert.That(updated, Is.True);
			Assert.That(previewReceived, Is.True);
			Assert.That(messages.Any(message => message.ExecutionState?.Status == ExecutionStatusDto.Failed), Is.False);
			Assert.That(messages.Select(message => message.ExecutionId), Is.All.EqualTo(runner.ExecutionId));
		}
	}

	[Test]
	public async Task ContinuousExecution_ShouldThrottleTelemetryMessages()
	{
		var messages = new List<ExecutionMessageDto>();
		var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var request = CreateSingleGeneratorRequest(showPreview: false);
		request.Mode = ExecutionModeDto.Continuous;
		var runner = CreateRunner(request, messages, completed);

		runner.Start();

		var started = false;
		for (var attempt = 0; attempt < 100 && !started; attempt++)
		{
			lock (messages)
			{
				started = messages.Any(message =>
					message.ExecutionState?.Status == ExecutionStatusDto.Running);
			}

			if (!started)
				await Task.Delay(10);
		}

		var telemetryWindow = Stopwatch.StartNew();
		await Task.Delay(500);
		telemetryWindow.Stop();
		await runner.DisposeAsync();

		List<ExecutionMessageDto> snapshot;
		lock (messages)
			snapshot = [.. messages];

		var runningTelemetryCount = snapshot.Count(message =>
			message.ExecutionState?.Status == ExecutionStatusDto.Running &&
			string.Equals(message.ExecutionState.Message, "Executing.", StringComparison.Ordinal));
		var nodeUpdateCount = snapshot.Count(message => message.MessageType == ExecutionMessageTypeDto.NodeUpdate);
		var expectedTelemetryLimit = (int)Math.Ceiling(telemetryWindow.Elapsed.TotalMilliseconds / 100.0) + 3;

		using (Assert.EnterMultipleScope())
		{
			Assert.That(started, Is.True);
			Assert.That(runningTelemetryCount, Is.LessThanOrEqualTo(expectedTelemetryLimit));
			Assert.That(nodeUpdateCount, Is.LessThanOrEqualTo(expectedTelemetryLimit));
			Assert.That(snapshot.Any(message => message.ExecutionState?.Status == ExecutionStatusDto.Failed), Is.False);
		}
	}

	[Test]
	public async Task ContinuousExecution_WithoutPreviewSubscribers_ShouldSkipImagePreviewMessages()
	{
		var messages = new List<ExecutionMessageDto>();
		var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var request = CreateSingleGeneratorRequest(showPreview: true);
		request.Mode = ExecutionModeDto.Continuous;
		request.PreviewRefreshRate = 1001;
		var runner = CreateRunner(request, messages, completed, hasPreviewSubscribers: () => false);

		runner.Start();
		await Task.Delay(150);
		await runner.DisposeAsync();

		using (Assert.EnterMultipleScope())
		{
			Assert.That(messages.Any(message => message.MessageType == ExecutionMessageTypeDto.ImagePreview), Is.False);
			Assert.That(messages.Any(message => message.MessageType == ExecutionMessageTypeDto.Failure), Is.False);
		}
	}

	[Test]
	public async Task ContinuousExecution_WithSlowPreviewConsumer_ShouldKeepOnlyOnePreviewPerNodeInFlight()
	{
		var firstPreviewStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var releasePreview = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var request = CreateSingleGeneratorRequest(showPreview: true);
		request.Mode = ExecutionModeDto.Continuous;
		request.PreviewRefreshRate = 1001;
		var previewPublishCount = 0;
		var latestFrameCount = 0L;
		var graphFactory = new RuntimeGraphFactory(new RuntimeNodeCatalog());
		var runner = new GraphExecutionRunner(
			request,
			graphFactory,
			async (message, cancellationToken) =>
			{
				if (message.MessageType == ExecutionMessageTypeDto.ImagePreview)
				{
					Interlocked.Increment(ref previewPublishCount);
					firstPreviewStarted.TrySetResult();
					await releasePreview.Task.WaitAsync(cancellationToken);
					return;
				}

				if (message.ExecutionState is
					{
						Status: ExecutionStatusDto.Running,
						Message: "Executing."
					} state)
				{
					Interlocked.Exchange(ref latestFrameCount, state.FramesProcessed);
				}
			},
			_ => { });

		try
		{
			runner.Start();
			await firstPreviewStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
			await Task.Delay(350);

			using (Assert.EnterMultipleScope())
			{
				Assert.That(Volatile.Read(ref previewPublishCount), Is.EqualTo(1));
				Assert.That(Volatile.Read(ref latestFrameCount), Is.GreaterThan(1));
			}
		}
		finally
		{
			releasePreview.TrySetResult();
			await runner.DisposeAsync();
		}
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
		using (Assert.EnterMultipleScope())
		{
			Assert.That(imagePreview!.Width, Is.EqualTo(32));
			Assert.That(imagePreview.Height, Is.EqualTo(16));
			Assert.That(imagePreview.PreviewWidth, Is.EqualTo(expectedPreviewWidth));
			Assert.That(imagePreview.PreviewHeight, Is.EqualTo(expectedPreviewHeight));
			Assert.That(imagePreview.Stride, Is.EqualTo(expectedPreviewWidth));
		}
	}

	private static GraphExecutionRunner CreateRunner(
		ExecutionRequestDto request,
		List<ExecutionMessageDto> messages,
		TaskCompletionSource completed,
		Func<bool>? hasPreviewSubscribers = null)
	{
		var graphFactory = new RuntimeGraphFactory(new RuntimeNodeCatalog());
		return new GraphExecutionRunner(
			request,
			graphFactory,
			(message, _) =>
			{
				lock (messages)
					messages.Add(message);

				if (message.MessageType is ExecutionMessageTypeDto.Completed or ExecutionMessageTypeDto.Failure)
					completed.TrySetResult();

				return Task.CompletedTask;
			},
			_ => completed.TrySetResult(),
			hasPreviewSubscribers);
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

	private static ExecutionRequestDto CreateTerminalCompletionRequest()
		=> new()
		{
			ClientId = "test-client",
			Mode = ExecutionModeDto.Continuous,
			Graph = new GraphDto
			{
				Nodes =
				[
					new NodeDto
					{
						Id = "trigger",
						Type = nameof(TimeTriggerNode),
						Properties = [new NodePropertyDto { Name = nameof(TimeTriggerNode.FramesPerSecond), Value = "2" }]
					},
					new NodeDto { Id = "triggered-terminal", Type = nameof(ImageGeneratorNode) },
					new NodeDto { Id = "continuous-terminal", Type = nameof(ImageGeneratorNode) }
				],
				Connections =
				[
					new ConnectionDto
					{
						OutputNodeId = "trigger",
						OutputPortName = "Trigger",
						InputNodeId = "triggered-terminal",
						InputPortName = "Trigger"
					}
				]
			}
		};
}
