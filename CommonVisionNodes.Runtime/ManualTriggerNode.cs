namespace CommonVisionNodes.Runtime
{
	/// <summary>
	/// Emits a trigger signal when requested by the host application.
	/// </summary>
	public sealed class ManualTriggerNode : Node
	{
		private int _pendingTriggerCount;

		/// <summary>
		/// Output port that emits trigger signals.
		/// </summary>
		public Port TriggerOutput { get; }

		/// <summary>
		/// Number of trigger signals emitted by this node.
		/// </summary>
		public int TriggerCount { get; private set; }

		internal string TriggerId { get; set; } = string.Empty;

		internal Func<string, bool>? TryConsumeExternalTrigger { get; set; }

		/// <summary>
		/// Creates a manual trigger node with one trigger output.
		/// </summary>
		public ManualTriggerNode()
		{
			TriggerOutput = AddOutput("Trigger", typeof(TriggerSignal), "Trigger signal emitted when the manual trigger is pressed.");
		}

		/// <summary>
		/// Queues one trigger signal for the next graph execution cycle.
		/// </summary>
		public void Trigger()
		{
			Interlocked.Increment(ref _pendingTriggerCount);
		}

		/// <inheritdoc/>
		public override void Execute()
		{
			var shouldTrigger = ConsumeLocalTrigger()
				|| (TryConsumeExternalTrigger?.Invoke(TriggerId) ?? false);

			if (shouldTrigger)
				TriggerCount++;

			TriggerOutput.Value = shouldTrigger
				? TriggerSignal.Active
				: TriggerSignal.Inactive;
		}

		/// <inheritdoc/>
		public override void EmitCode(CodeEmitContext context)
		{
			context.Builder.AppendLine("// Manual trigger is a runtime execution-control node.");
		}

		private bool ConsumeLocalTrigger()
		{
			while (true)
			{
				var current = Volatile.Read(ref _pendingTriggerCount);
				if (current <= 0)
					return false;

				if (Interlocked.CompareExchange(ref _pendingTriggerCount, current - 1, current) == current)
					return true;
			}
		}
	}
}
