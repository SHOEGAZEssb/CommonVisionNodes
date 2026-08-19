namespace CommonVisionNodes.Runtime
{
	/// <summary>
	/// An inspection node that accepts any value from a connected port, exposes it for visual inspection,
	/// and passes it through unchanged.
	/// The UI renders the value differently depending on its runtime type:
	/// <list type="bullet">
	///   <item><see cref="Stemmer.Cvb.Image"/>: rendered as an image preview.</item>
	///   <item><see cref="IReadOnlyList{BlobInfo}"/> or <see cref="IReadOnlyList{BlobRect}"/>: rendered as a string list.</item>
	///   <item><see cref="IReadOnlyList{PolimagoClassifyResultItem}"/>: rendered as a string list.</item>
	///   <item>Anything else: rendered via <see cref="object.ToString"/>.</item>
	/// </list>
	/// </summary>
	public sealed class GenericVisualizerNode : Node
	{
		/// <summary>
		/// Input port that accepts any value type.
		/// </summary>
		public Port DataInput { get; }

		/// <summary>
		/// Output port that passes the received value through unchanged.
		/// </summary>
		public Port DataOutput { get; }

		/// <summary>
		/// The value received during the last execution, or <c>null</c> if not yet executed.
		/// </summary>
		public object? LastValue { get; private set; }

		/// <summary>
		/// Creates a generic visualizer node with object input and pass-through output.
		/// </summary>
		public GenericVisualizerNode()
		{
			DataInput = AddInput("Data", typeof(object),
				"Any data value to visualize. Supports Image, blob lists, and classification results.");
			DataOutput = AddOutput("Data", typeof(object),
				"The received data value, passed through unchanged.");
		}

		/// <inheritdoc/>
		public override void Execute()
		{
			LastValue = DataInput.Value;
			DataOutput.Value = LastValue;
		}

		// Code generation

		/// <inheritdoc/>
		public override void EmitCode(CodeEmitContext context)
		{
			var inputVar = context.ResolveInput(DataInput);
			if (inputVar != null)
				context.RegisterOutput(DataOutput, inputVar);
		}
	}
}
