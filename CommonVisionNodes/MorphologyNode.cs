using System.Text;
using Stemmer.Cvb;

namespace CommonVisionNodes
{
    /// <summary>
    /// Available morphological operations for <see cref="MorphologyNode"/>.
    /// </summary>
    public enum MorphologyOperation
    {
        /// <summary>
        /// Expands bright regions (maximum filter).
        /// </summary>
        Dilate,

        /// <summary>
        /// Shrinks bright regions (minimum filter).
        /// </summary>
        Erode,

        /// <summary>
        /// Erosion followed by dilation — removes small bright spots.
        /// </summary>
        Open,

        /// <summary>
        /// Dilation followed by erosion — fills small dark gaps.
        /// </summary>
        Close
    }

    /// <summary>
    /// Applies a morphological operation (dilate, erode, open, close) to the input image
    /// using a square structuring element.
    /// </summary>
    public sealed class MorphologyNode : Node
    {
        private Image? _lastResult;

        /// <summary>
        /// Input port that receives the source image.
        /// </summary>
        public Port ImageInput { get; }

        /// <summary>
        /// Output port that provides the morphologically processed image.
        /// </summary>
        public Port ImageOutput { get; }

        /// <summary>
        /// The morphological operation to apply.
        /// </summary>
        public MorphologyOperation Operation { get; set; } = MorphologyOperation.Dilate;

        /// <summary>
        /// Kernel size of the structuring element.
        /// </summary>
        public KernelSize KernelSize { get; set; } = KernelSize.Kernel3x3;

        public MorphologyNode()
        {
            ImageInput = AddInput("Image", typeof(Image), "The source image (typically binary) to process.");
            ImageOutput = AddOutput("Image", typeof(Image), "The morphologically processed image.");
        }

        /// <inheritdoc/>
        public override void Execute()
        {
            var source = (Image)ImageInput.Value!;
            int radius = KernelSizeToRadius(KernelSize);

            _lastResult?.Dispose();
            _lastResult = Operation switch
            {
                MorphologyOperation.Erode => ApplyMorphology(source, radius, erode: true),
                MorphologyOperation.Open => ApplyOpenClose(source, radius, openOp: true),
                MorphologyOperation.Close => ApplyOpenClose(source, radius, openOp: false),
                _ => ApplyMorphology(source, radius, erode: false)
            };

            ImageOutput.Value = _lastResult;
        }

        private static int KernelSizeToRadius(KernelSize size) => size switch
        {
            KernelSize.Kernel5x5 => 2,
            KernelSize.Kernel7x7 => 3,
            _ => 1
        };

        private static Image ApplyOpenClose(Image source, int radius, bool openOp)
        {
            using var intermediate = openOp
                ? ApplyMorphology(source, radius, erode: true)
                : ApplyMorphology(source, radius, erode: false);
            return openOp
                ? ApplyMorphology(intermediate, radius, erode: false)
                : ApplyMorphology(intermediate, radius, erode: true);
        }

        private static Image ApplyMorphology(Image source, int radius, bool erode)
        {
            int width = source.Width;
            int height = source.Height;
            var result = new Image(source.Size, source.Planes.Count);

            for (int p = 0; p < source.Planes.Count; p++)
            {
                var srcAccess = source.Planes[p].GetLinearAccess();
                var dstAccess = result.Planes[p].GetLinearAccess();

                unsafe
                {
                    byte* srcBase = (byte*)srcAccess.BasePtr;
                    byte* dstBase = (byte*)dstAccess.BasePtr;
                    long srcYInc = srcAccess.YInc;
                    long srcXInc = srcAccess.XInc;
                    long dstYInc = dstAccess.YInc;
                    long dstXInc = dstAccess.XInc;

                    for (int y = 0; y < height; y++)
                    {
                        byte* dstRow = dstBase + y * dstYInc;
                        for (int x = 0; x < width; x++)
                        {
                            byte extremum = erode ? byte.MaxValue : byte.MinValue;
                            for (int ky = -radius; ky <= radius; ky++)
                            {
                                int sy = Math.Clamp(y + ky, 0, height - 1);
                                for (int kx = -radius; kx <= radius; kx++)
                                {
                                    int sx = Math.Clamp(x + kx, 0, width - 1);
                                    byte val = *(srcBase + sy * srcYInc + sx * srcXInc);
                                    extremum = erode ? Math.Min(extremum, val) : Math.Max(extremum, val);
                                }
                            }
                            *(dstRow + x * dstXInc) = extremum;
                        }
                    }
                }
            }

            return result;
        }

        private static string KernelSizeToCodeDimension(KernelSize size) => size switch
        {
            KernelSize.Kernel5x5 => "2",
            KernelSize.Kernel7x7 => "3",
            _ => "1"
        };

        // Code generation

        /// <inheritdoc/>
        public override string CodeVariableName => "morphed";

        /// <inheritdoc/>
        public override IReadOnlyList<string> RequiredUsings => ["System.Runtime.InteropServices"];

        /// <inheritdoc/>
        public override void EmitCode(CodeEmitContext context)
        {
            var inputVar = context.ResolveInput(ImageInput);
            if (inputVar == null) return;

            var varName = context.GetUniqueVariable(CodeVariableName);
            var sb = context.Builder;
            var radiusLiteral = KernelSizeToCodeDimension(KernelSize);

            sb.AppendLine($"// Morphology: {Operation} (kernel: {KernelSize})");
            var call = Operation switch
            {
                MorphologyOperation.Erode => $"MorphologyOp({inputVar}, {radiusLiteral}, erode: true)",
                MorphologyOperation.Open => $"MorphologyOpenClose({inputVar}, {radiusLiteral}, openOp: true)",
                MorphologyOperation.Close => $"MorphologyOpenClose({inputVar}, {radiusLiteral}, openOp: false)",
                _ => $"MorphologyOp({inputVar}, {radiusLiteral}, erode: false)"
            };
            sb.AppendLine($"using var {varName} = {call};");
            context.RegisterOutput(ImageOutput, varName);
        }

        /// <inheritdoc/>
        public override void EmitHelperMethods(StringBuilder sb)
        {
            sb.AppendLine("static Image MorphologyOpenClose(Image source, int radius, bool openOp)");
            sb.AppendLine("{");
            sb.AppendLine("    using var intermediate = openOp");
            sb.AppendLine("        ? MorphologyOp(source, radius, erode: true)");
            sb.AppendLine("        : MorphologyOp(source, radius, erode: false);");
            sb.AppendLine("    return openOp");
            sb.AppendLine("        ? MorphologyOp(intermediate, radius, erode: false)");
            sb.AppendLine("        : MorphologyOp(intermediate, radius, erode: true);");
            sb.AppendLine("}");
            sb.AppendLine();
            sb.AppendLine("static Image MorphologyOp(Image source, int radius, bool erode)");
            sb.AppendLine("{");
            sb.AppendLine("    int width = source.Width;");
            sb.AppendLine("    int height = source.Height;");
            sb.AppendLine("    var result = new Image(source.Size, source.Planes.Count);");
            sb.AppendLine("    for (int p = 0; p < source.Planes.Count; p++)");
            sb.AppendLine("    {");
            sb.AppendLine("        var srcAccess = source.Planes[p].GetLinearAccess();");
            sb.AppendLine("        var dstAccess = result.Planes[p].GetLinearAccess();");
            sb.AppendLine("        for (int y = 0; y < height; y++)");
            sb.AppendLine("        {");
            sb.AppendLine("            for (int x = 0; x < width; x++)");
            sb.AppendLine("            {");
            sb.AppendLine("                byte extremum = erode ? byte.MaxValue : byte.MinValue;");
            sb.AppendLine("                for (int ky = -radius; ky <= radius; ky++)");
            sb.AppendLine("                {");
            sb.AppendLine("                    int sy = Math.Clamp(y + ky, 0, height - 1);");
            sb.AppendLine("                    for (int kx = -radius; kx <= radius; kx++)");
            sb.AppendLine("                    {");
            sb.AppendLine("                        int sx = Math.Clamp(x + kx, 0, width - 1);");
            sb.AppendLine("                        var ptr = srcAccess.BasePtr + (nint)(sy * srcAccess.YInc + sx * srcAccess.XInc);");
            sb.AppendLine("                        byte val = Marshal.ReadByte(ptr);");
            sb.AppendLine("                        extremum = erode ? Math.Min(extremum, val) : Math.Max(extremum, val);");
            sb.AppendLine("                    }");
            sb.AppendLine("                }");
            sb.AppendLine("                var dstPtr = dstAccess.BasePtr + (nint)(y * dstAccess.YInc + x * dstAccess.XInc);");
            sb.AppendLine("                Marshal.WriteByte(dstPtr, extremum);");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("    return result;");
            sb.AppendLine("}");
        }
    }
}
