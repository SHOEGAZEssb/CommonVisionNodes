namespace CommonVisionNodes
{
    /// <summary>
    /// A sink node that accepts any value from a connected port and exposes it for visual inspection.
    /// The UI renders the value differently depending on its runtime type:
    /// <list type="bullet">
    ///   <item><see cref="Stemmer.Cvb.Image"/> — rendered as an image preview.</item>
    ///   <item><see cref="IReadOnlyList{BlobInfo}"/> or <see cref="IReadOnlyList{BlobRect}"/> — rendered as a string list.</item>
    ///   <item><see cref="IReadOnlyList{PolimagoClassifyResultItem}"/> — rendered as a string list.</item>
    ///   <item>Anything else — rendered via <see cref="object.ToString"/>.</item>
    /// </list>
    /// </summary>
    public sealed class GenericVisualizerNode : Node
    {
        /// <summary>
        /// Input port that accepts any value type.
        /// </summary>
        public Port DataInput { get; }

        /// <summary>
        /// The value received during the last execution, or <c>null</c> if not yet executed.
        /// </summary>
        public object? LastValue { get; private set; }

        public GenericVisualizerNode()
        {
            DataInput = AddInput("Data", typeof(object),
                "Any data value to visualize. Supports Image, blob lists, and classification results.");
        }

        /// <inheritdoc/>
        public override void Execute()
        {
            LastValue = DataInput.Value;
        }
    }
}
