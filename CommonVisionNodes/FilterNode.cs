using Stemmer.Cvb;
using Stemmer.Cvb.Foundation;

namespace CommonVisionNodes
{
    /// <summary>
    /// Available filter types for <see cref="FilterNode"/>.
    /// </summary>
    public enum FilterType
    {
        /// <summary>
        /// Gaussian blur for noise reduction.
        /// </summary>
        Gauss,

        /// <summary>
        /// Low-pass filter (smoothing).
        /// </summary>
        LowPass,

        /// <summary>
        /// High-pass filter (edge enhancement).
        /// </summary>
        HighPass,

        /// <summary>
        /// Sharpening filter.
        /// </summary>
        Sharpen,

        /// <summary>
        /// Laplace edge detection.
        /// </summary>
        Laplace,

        /// <summary>
        /// Sobel edge detection (horizontal).
        /// </summary>
        SobelH,

        /// <summary>
        /// Sobel edge detection (vertical).
        /// </summary>
        SobelV,

        /// <summary>
        /// Box median filter for noise reduction.
        /// </summary>
        Median,

        /// <summary>
        /// Wiener filter for noise reduction.
        /// </summary>
        Wiener
    }

    /// <summary>
    /// Kernel sizes available for image filters.
    /// </summary>
    public enum KernelSize
    {
        /// <summary>3×3 kernel.</summary>
        Kernel3x3,
        /// <summary>5×5 kernel.</summary>
        Kernel5x5,
        /// <summary>7×7 kernel.</summary>
        Kernel7x7
    }

    /// <summary>
    /// Applies a configurable image filter using the CVB Foundation library.
    /// </summary>
    public sealed class FilterNode : Node
    {
        private Image? _lastResult;

        /// <summary>
        /// Input port that receives the source image.
        /// </summary>
        public Port ImageInput { get; }

        /// <summary>
        /// Output port that provides the filtered image.
        /// </summary>
        public Port ImageOutput { get; }

        /// <summary>
        /// The filter algorithm to apply.
        /// </summary>
        public FilterType FilterType { get; set; } = FilterType.Gauss;

        /// <summary>
        /// Kernel size used by most filters (3×3, 5×5, or 7×7).
        /// </summary>
        public KernelSize KernelSize { get; set; } = KernelSize.Kernel3x3;

        public FilterNode()
        {
            ImageInput = AddInput("Image", typeof(Image));
            ImageOutput = AddOutput("Image", typeof(Image));
        }

        /// <inheritdoc/>
        public override void Execute()
        {
            var source = (Image)ImageInput.Value!;

            var filterType = FilterType;
            var fixedSize = ToFixedFilterSize(KernelSize);

            _lastResult?.Dispose();
            _lastResult = filterType switch
            {
                FilterType.Gauss => Filter.Gauss(source, fixedSize),
                FilterType.LowPass => Filter.LowPass(source, fixedSize),
                FilterType.HighPass => Filter.HighPass(source, fixedSize),
                FilterType.Sharpen => Filter.Sharpen(source),
                FilterType.Laplace => Filter.Laplace(source, fixedSize),
                FilterType.SobelH => Filter.Sobel(source, FilterOrientation.Horizontal, fixedSize),
                FilterType.SobelV => Filter.Sobel(source, FilterOrientation.Vertical, fixedSize),
                FilterType.Median => Filter.BoxMedian(source, ToSize2D(KernelSize)),
                FilterType.Wiener => Filter.Wiener(source, ToSize2D(KernelSize)),
                _ => Filter.Gauss(source, fixedSize)
            };

            ImageOutput.Value = _lastResult;
        }

        private static FixedFilterSize ToFixedFilterSize(KernelSize size) => size switch
        {
            KernelSize.Kernel5x5 => FixedFilterSize.Kernel5x5,
            KernelSize.Kernel7x7 => FixedFilterSize.Kernel7x7,
            _ => FixedFilterSize.Kernel3x3
        };

        private static Size2D ToSize2D(KernelSize size) => size switch
        {
            KernelSize.Kernel5x5 => new Size2D(5, 5),
            KernelSize.Kernel7x7 => new Size2D(7, 7),
            _ => new Size2D(3, 3)
        };

        private static string KernelSizeToCodeLiteral(KernelSize size) => size switch
        {
            KernelSize.Kernel5x5 => "Kernel5x5",
            KernelSize.Kernel7x7 => "Kernel7x7",
            _ => "Kernel3x3"
        };

        private static (int W, int H) KernelSizeToDimensions(KernelSize size) => size switch
        {
            KernelSize.Kernel5x5 => (5, 5),
            KernelSize.Kernel7x7 => (7, 7),
            _ => (3, 3)
        };

        // Code generation

        /// <inheritdoc/>
        public override string CodeVariableName => "filtered";

        /// <inheritdoc/>
        public override IReadOnlyList<string> RequiredUsings => ["Stemmer.Cvb.Foundation"];

        /// <inheritdoc/>
        public override void EmitCode(CodeEmitContext context)
        {
            var inputVar = context.ResolveInput(ImageInput);
            if (inputVar == null) return;

            var varName = context.GetUniqueVariable(CodeVariableName);
            var sb = context.Builder;
            var ks = KernelSizeToCodeLiteral(KernelSize);
            var (w, h) = KernelSizeToDimensions(KernelSize);

            sb.AppendLine($"// Apply {FilterType} filter (kernel: {KernelSize})");
            var call = FilterType switch
            {
                FilterType.Gauss => $"Filter.Gauss({inputVar}, FixedFilterSize.{ks})",
                FilterType.LowPass => $"Filter.LowPass({inputVar}, FixedFilterSize.{ks})",
                FilterType.HighPass => $"Filter.HighPass({inputVar}, FixedFilterSize.{ks})",
                FilterType.Sharpen => $"Filter.Sharpen({inputVar})",
                FilterType.Laplace => $"Filter.Laplace({inputVar}, FixedFilterSize.{ks})",
                FilterType.SobelH => $"Filter.Sobel({inputVar}, FilterOrientation.Horizontal, FixedFilterSize.{ks})",
                FilterType.SobelV => $"Filter.Sobel({inputVar}, FilterOrientation.Vertical, FixedFilterSize.{ks})",
                FilterType.Median => $"Filter.BoxMedian({inputVar}, new Size2D({w}, {h}))",
                FilterType.Wiener => $"Filter.Wiener({inputVar}, new Size2D({w}, {h}))",
                _ => $"Filter.Gauss({inputVar}, FixedFilterSize.{ks})"
            };
            sb.AppendLine($"using var {varName} = {call};");
            context.RegisterOutput(ImageOutput, varName);
        }
    }
}
