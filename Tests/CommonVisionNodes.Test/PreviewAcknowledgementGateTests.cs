using CommonVisionNodes.Contracts;
using CommonVisionNodes.Server.Services;

namespace CommonVisionNodes.Test;

public sealed class PreviewAcknowledgementGateTests
{
	[Test]
	public void Begin_WhenAcknowledgementsAreDisabled_ShouldNotWait()
	{
		var gate = new PreviewAcknowledgementGate();

		var acknowledgementTask = gate.Begin(CreatePreviewMessage(sequence: 1));

		Assert.That(acknowledgementTask, Is.Null);
	}

	[Test]
	public async Task TryAcknowledge_WithMatchingPreview_ShouldCompleteWait()
	{
		var gate = new PreviewAcknowledgementGate();
		gate.Configure(isEnabled: true);
		var acknowledgementTask = gate.Begin(CreatePreviewMessage(sequence: 42));

		var acknowledged = gate.TryAcknowledge(CreateAcknowledgement(sequence: 42));

		using (Assert.EnterMultipleScope())
		{
			Assert.That(acknowledged, Is.True);
			Assert.That(acknowledgementTask, Is.Not.Null);
		}

		await acknowledgementTask!.WaitAsync(TimeSpan.FromSeconds(1));
	}

	[Test]
	public void TryAcknowledge_WithStaleSequence_ShouldKeepCurrentWaitPending()
	{
		var gate = new PreviewAcknowledgementGate();
		gate.Configure(isEnabled: true);
		var acknowledgementTask = gate.Begin(CreatePreviewMessage(sequence: 42));

		var acknowledged = gate.TryAcknowledge(CreateAcknowledgement(sequence: 41));

		using (Assert.EnterMultipleScope())
		{
			Assert.That(acknowledged, Is.False);
			Assert.That(acknowledgementTask, Is.Not.Null);
			Assert.That(acknowledgementTask!.IsCompleted, Is.False);
		}

		gate.CancelPending();
	}

	[Test]
	public void Configure_WhenDisabled_ShouldCancelPendingWait()
	{
		var gate = new PreviewAcknowledgementGate();
		gate.Configure(isEnabled: true);
		var acknowledgementTask = gate.Begin(CreatePreviewMessage(sequence: 1));

		gate.Configure(isEnabled: false);

		Assert.That(acknowledgementTask, Is.Not.Null);
		Assert.That(acknowledgementTask!.IsCanceled, Is.True);
	}

	private static ExecutionMessageDto CreatePreviewMessage(long sequence)
		=> new()
		{
			ExecutionId = "execution",
			MessageType = ExecutionMessageTypeDto.ImagePreview,
			ImagePreview = new ImagePreviewDto
			{
				NodeId = "node",
				PreviewSequence = sequence
			}
		};

	private static PreviewClientMessageDto CreateAcknowledgement(long sequence)
		=> new()
		{
			MessageType = PreviewClientMessageTypeDto.Acknowledge,
			ExecutionId = "execution",
			NodeId = "node",
			PreviewSequence = sequence
		};
}
