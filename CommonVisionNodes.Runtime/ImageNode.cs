using Stemmer.Cvb;

namespace CommonVisionNodes
{
    /// <summary>
    /// Loads an image from a file and provides it as output.
    /// </summary>
    public sealed class ImageNode : Node, IInitializable, ITriggerableNode
    {
        private Image? _cachedImage;

        /// <summary>
        /// Optional trigger input that gates when the image is sent downstream.
        /// </summary>
        public Port TriggerInput { get; }

        /// <summary>
        /// The loaded image, available after initialization.
        /// </summary>
        public Image? CachedImage => _cachedImage;

        /// <summary>
        /// Output port that provides the loaded image.
        /// </summary>
        public Port ImageOutput { get; }

        /// <summary>
        /// Path to the image file to load.
        /// </summary>
        public string FilePath { get; set; } = string.Empty;

        /// <inheritdoc/>
        public bool IsInitialized { get; private set; }

        /// <summary>
        /// Creates an image source node with an optional trigger input and one image output.
        /// </summary>
        public ImageNode()
        {
            TriggerInput = AddInput("Trigger", typeof(TriggerSignal), "Optional trigger that controls when the image is sent.");
            ImageOutput = AddOutput("Image", typeof(Image), "The image loaded from the configured file path.");
        }

        /// <inheritdoc/>
        public void Initialize()
        {
            _cachedImage?.Dispose();
            _cachedImage = Image.FromFile(FilePath);
            IsInitialized = true;
        }

        /// <inheritdoc/>
        public override void Execute()
        {
            if (!IsInitialized)
                throw new InvalidOperationException($"{nameof(ImageNode)} must be initialized before execution.");

            ImageOutput.Value = _cachedImage;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            _cachedImage?.Dispose();
            _cachedImage = null;
            IsInitialized = false;
        }

        // Code generation

        /// <inheritdoc/>
        public override string CodeVariableName => "sourceImage";

        /// <inheritdoc/>
        public override void EmitCode(CodeEmitContext context)
        {
            var varName = context.GetUniqueVariable(CodeVariableName);
            context.Builder.AppendLine("// Load image from file");
            context.Builder.AppendLine($"using var {varName} = Image.FromFile(@\"{CodeEmitContext.EscapeVerbatim(FilePath)}\");");
            context.RegisterOutput(ImageOutput, varName);
        }
    }
}
