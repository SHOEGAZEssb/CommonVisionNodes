using System.Diagnostics;

namespace CommonVisionNodes.Runtime
{
    /// <summary>
    /// Emits a trigger signal at a fixed interval.
    /// </summary>
    public sealed class TimeTriggerNode : Node
    {
        private double _framesPerSecond = 1.0;
        private long _lastTriggerTimestamp;
        private bool _hasTriggered;

        /// <summary>
        /// Output port that emits trigger signals.
        /// </summary>
        public Port TriggerOutput { get; }

        /// <summary>
        /// Trigger rate in frames per second.
        /// </summary>
        public double FramesPerSecond
        {
            get => _framesPerSecond;
            set
            {
                if (!double.IsFinite(value))
                    return;

                _framesPerSecond = Math.Max(0.0, value);
            }
        }

        /// <summary>
        /// Creates a time trigger node with one trigger output.
        /// </summary>
        public TimeTriggerNode()
        {
            TriggerOutput = AddOutput("Trigger", typeof(TriggerSignal), "Trigger signal emitted on the configured interval.");
        }

        /// <inheritdoc/>
        public override void Execute()
        {
            var now = Stopwatch.GetTimestamp();
            var framesPerSecond = FramesPerSecond;
            var intervalSeconds = framesPerSecond <= 0.0 ? 0.0 : 1.0 / framesPerSecond;

            var shouldTrigger = !_hasTriggered
                || intervalSeconds <= 0.0
                || (now - _lastTriggerTimestamp) / (double)Stopwatch.Frequency >= intervalSeconds;

            if (shouldTrigger)
            {
                _hasTriggered = true;
                _lastTriggerTimestamp = now;
            }

            TriggerOutput.Value = shouldTrigger
                ? TriggerSignal.Active
                : TriggerSignal.Inactive;
        }

        /// <inheritdoc/>
        public override void EmitCode(CodeEmitContext context)
        {
            context.Builder.AppendLine("// Time trigger is a runtime execution-control node.");
        }
    }
}
