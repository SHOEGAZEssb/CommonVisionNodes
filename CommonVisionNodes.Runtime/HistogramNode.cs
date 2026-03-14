using Stemmer.Cvb;
using Stemmer.Cvb.Foundation;

namespace CommonVisionNodes
{
    /// <summary>
    /// Computes the histogram of the input image and passes the image through.
    /// Histogram bin data is exposed for visualization.
    /// </summary>
    public sealed class HistogramNode : Node
    {
        /// <summary>
        /// Input port that receives the source image.
        /// </summary>
        public Port ImageInput { get; }

        /// <summary>
        /// Output port that passes the source image through.
        /// </summary>
        public Port ImageOutput { get; }

        /// <summary>
        /// Histogram bin values for the first plane (256 entries for 8-bit images).
        /// Updated after each <see cref="Execute"/> call.
        /// </summary>
        public long[] Bins { get; private set; } = [];

        /// <summary>
        /// Mean intensity value from the histogram.
        /// </summary>
        public double Mean { get; private set; }

        /// <summary>
        /// Standard deviation from the histogram.
        /// </summary>
        public double StdDev { get; private set; }

        public HistogramNode()
        {
            ImageInput = AddInput("Image", typeof(Image), "The source image to analyze.");
            ImageOutput = AddOutput("Image", typeof(Image), "The source image passed through unchanged.");
        }

        /// <inheritdoc/>
        public override void Execute()
        {
            var source = (Image)ImageInput.Value!;

            var histogram = HistogramAnalyzer.Create(source.Planes[0]);
            int count = histogram.Count;
            var bins = new long[count];
            for (int i = 0; i < count; i++)
                bins[i] = histogram[i];

            Bins = bins;
            Mean = histogram.Mean;
            StdDev = histogram.StandardDeviation;

            ImageOutput.Value = source;
        }

        // Code generation

        /// <inheritdoc/>
        public override string CodeVariableName => "histogram";

        /// <inheritdoc/>
        public override IReadOnlyList<string> RequiredUsings => ["Stemmer.Cvb.Foundation"];

        /// <inheritdoc/>
        public override void EmitCode(CodeEmitContext context)
        {
            var inputVar = context.ResolveInput(ImageInput);
            if (inputVar == null) return;

            var varName = context.GetUniqueVariable(CodeVariableName);
            var sb = context.Builder;
            sb.AppendLine("// Compute histogram of the first plane");
            sb.AppendLine($"var {varName} = HistogramAnalyzer.Create({inputVar}.Planes[0]);");
            sb.AppendLine($"Console.WriteLine($\"Mean: {{{varName}.Mean:F2}}  StdDev: {{{varName}.StandardDeviation:F2}}\");");
            context.RegisterOutput(ImageOutput, inputVar);
        }
    }
}
