namespace CommonVisionNodes
{
    /// <summary>
    /// Represents an execution trigger produced by trigger nodes.
    /// </summary>
    public readonly record struct TriggerSignal(bool IsTriggered)
    {
        /// <summary>
        /// A trigger that is active for the current graph execution cycle.
        /// </summary>
        public static TriggerSignal Active { get; } = new(true);

        /// <summary>
        /// A trigger that is inactive for the current graph execution cycle.
        /// </summary>
        public static TriggerSignal Inactive { get; } = new(false);
    }
}
