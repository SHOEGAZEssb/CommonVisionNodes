using Stemmer.Cvb;

namespace CommonVisionNodes
{
    /// <summary>
    /// Saves the input image to a file.
    /// </summary>
    public sealed class SaveImageNode : Node
    {
        /// <summary>
        /// Input port that receives the image to save.
        /// </summary>
        public Port ImageInput { get; }

        /// <summary>
        /// Destination file path for the saved image.
        /// </summary>
        public string FilePath { get; set; } = string.Empty;

        public SaveImageNode()
        {
            ImageInput = AddInput("Image", typeof(Image));
        }

        /// <inheritdoc/>
        public override void Execute()
        {
            if (string.IsNullOrWhiteSpace(FilePath))
                return;

            var image = (Image)ImageInput.Value!;
            image.Save(FilePath);
        }

        // Code generation

        /// <inheritdoc/>
        public override void EmitCode(CodeEmitContext context)
        {
            var inputVar = context.ResolveInput(ImageInput);
            if (inputVar == null) return;

            context.Builder.AppendLine("// Save image to file");
            context.Builder.AppendLine($"{inputVar}.Save(@\"{CodeEmitContext.EscapeVerbatim(FilePath)}\");");
        }
    }
}
