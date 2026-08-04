using CommonVisionNodes.Contracts;
using CommonVisionNodes.Runtime;
using CommonVisionNodes.Runtime.Definitions;
using CommonVisionNodes.Runtime.Execution;
using CommonVisionNodes.Server.Services;

namespace CommonVisionNodes.Test;

public sealed class ExecutionClientManagerTests
{
	[Test]
	public void UnknownClientRequests_ShouldNotRetainSessions()
	{
		var manager = CreateManager();

		for (var index = 0; index < 100; index++)
		{
			var updated = manager.UpdateExecutionSettings(new UpdateExecutionSettingsRequestDto
			{
				ClientId = $"unknown-{index}",
				ExecutionId = "missing",
				PreviewRefreshRate = 30,
				PreviewImageMaxDimension = 640
			});

			Assert.That(updated, Is.False);
		}

		Assert.That(manager.SessionCount, Is.Zero);
	}

	[Test]
	public async Task CompletedExecution_ShouldReleaseIdleSession()
	{
		var manager = CreateManager();
		var clientId = Guid.NewGuid().ToString("N");

		await manager.StartExecutionAsync(
			new ExecutionRequestDto
			{
				ClientId = clientId,
				Mode = ExecutionModeDto.Single,
				Graph = new GraphDto
				{
					Nodes =
					[
						new NodeDto
						{
							Id = "generator",
							Type = nameof(ImageGeneratorNode)
						}
					]
				}
			},
			CancellationToken.None);

		using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
		while (manager.SessionCount != 0)
			await Task.Delay(10, timeout.Token);

		Assert.That(manager.SessionCount, Is.Zero);
	}

	private static ExecutionClientManager CreateManager()
	{
		var catalog = new RuntimeNodeCatalog();
		return new ExecutionClientManager(new RuntimeGraphFactory(catalog));
	}
}
