using Stemmer.Cvb;

namespace CommonVisionNodes
{
    public sealed class ImageNode : Node, IInitializable
    {
        private Image? _cachedImage;

        public Image? CachedImage => _cachedImage;

        public Port ImageOutput { get; }

        public string FilePath { get; set; } = string.Empty;

        public bool IsInitialized { get; private set; }

        public ImageNode()
        {
            ImageOutput = AddOutput("Image", typeof(Image));
        }

        public void Initialize()
        {
            _cachedImage?.Dispose();
            _cachedImage = Image.FromFile(FilePath);
            IsInitialized = true;
        }

        public override void Execute()
        {
            if (!IsInitialized)
                throw new InvalidOperationException($"{nameof(ImageNode)} must be initialized before execution.");

            ImageOutput.Value = _cachedImage;
        }

        public void Dispose()
        {
            _cachedImage?.Dispose();
            _cachedImage = null;
            IsInitialized = false;
        }

        // Code generation

        public override string CodeVariableName => "sourceImage";

        public override void EmitCode(CodeEmitContext context)
        {
            var varName = context.GetUniqueVariable(CodeVariableName);
            context.Builder.AppendLine("// Load image from file");
            context.Builder.AppendLine($"using var {varName} = Image.FromFile(@\"{CodeEmitContext.EscapeVerbatim(FilePath)}\");");
            context.RegisterOutput(ImageOutput, varName);
        }
    }
}
