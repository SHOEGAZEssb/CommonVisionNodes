namespace CommonVisionNodes
{
    /// <summary>
    /// Marks a node whose execution can be gated by a trigger input.
    /// </summary>
    public interface ITriggerableNode
    {
        /// <summary>
        /// Input port that receives trigger signals.
        /// </summary>
        Port TriggerInput { get; }
    }
}
