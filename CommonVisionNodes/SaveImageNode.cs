using Stemmer.Cvb;

namespace CommonVisionNodes
{
    public sealed class SaveImageNode : Node
    {
        public Port ImageInput { get; }

        public string FilePath { get; set; } = string.Empty;

        public SaveImageNode()
        {
            ImageInput = AddInput("Image", typeof(Image));
        }

        public override void Execute()
        {
            if (string.IsNullOrWhiteSpace(FilePath))
                return;

            var image = (Image)ImageInput.Value!;
            image.Save(FilePath);
        }

        // Code generation

        public override void EmitCode(CodeEmitContext context)
        {
            var inputVar = context.ResolveInput(ImageInput);
            if (inputVar == null) return;

            context.Builder.AppendLine("// Save image to file");
            context.Builder.AppendLine($"{inputVar}.Save(@\"{CodeEmitContext.EscapeVerbatim(FilePath)}\");");
        }
    }
}
