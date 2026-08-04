using CommonVisionNodes.Contracts;

namespace CommonVisionNodes.Server.Services;

/// <summary>
/// Tracks the single preview acknowledgement that can be outstanding for one WebSocket.
/// </summary>
internal sealed class PreviewAcknowledgementGate
{
	private readonly Lock _sync = new();
	private PendingPreview? _pending;
	private bool _isEnabled;

	/// <summary>
	/// Enables or disables acknowledgement pacing for this socket.
	/// </summary>
	public void Configure(bool isEnabled)
	{
		lock (_sync)
		{
			_isEnabled = isEnabled;
			if (!isEnabled)
				CancelPendingCore();
		}
	}

	/// <summary>
	/// Starts waiting for acknowledgement of an image message when pacing is enabled.
	/// </summary>
	/// <returns>The acknowledgement task, or <c>null</c> for clients without acknowledgement support.</returns>
	public Task? Begin(ExecutionMessageDto message)
	{
		ArgumentNullException.ThrowIfNull(message);

		if (!BinaryExecutionMessageCodec.TryGetImagePreview(message, out var imagePreview) ||
			imagePreview.PreviewSequence <= 0)
		{
			return null;
		}

		lock (_sync)
		{
			if (!_isEnabled)
				return null;

			CancelPendingCore();
			var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
			_pending = new PendingPreview(
				message.ExecutionId,
				imagePreview.NodeId,
				imagePreview.PreviewSequence,
				completion);
			return completion.Task;
		}
	}

	/// <summary>
	/// Completes the current wait when the acknowledgement identifies the outstanding preview.
	/// </summary>
	public bool TryAcknowledge(PreviewClientMessageDto acknowledgement)
	{
		ArgumentNullException.ThrowIfNull(acknowledgement);

		lock (_sync)
		{
			if (_pending is not { } pending ||
				!string.Equals(pending.ExecutionId, acknowledgement.ExecutionId, StringComparison.Ordinal) ||
				!string.Equals(pending.NodeId, acknowledgement.NodeId, StringComparison.Ordinal) ||
				pending.PreviewSequence != acknowledgement.PreviewSequence)
			{
				return false;
			}

			_pending = null;
			return pending.Completion.TrySetResult();
		}
	}

	/// <summary>
	/// Cancels any outstanding wait, for example when the socket disconnects or a wait times out.
	/// </summary>
	public void CancelPending()
	{
		lock (_sync)
			CancelPendingCore();
	}

	private void CancelPendingCore()
	{
		var pending = _pending;
		_pending = null;
		pending?.Completion.TrySetCanceled();
	}

	private sealed record PendingPreview(
		string ExecutionId,
		string NodeId,
		long PreviewSequence,
		TaskCompletionSource Completion);
}
