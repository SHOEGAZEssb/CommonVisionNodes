using System.Runtime.Intrinsics;
using System.Text;
using Stemmer.Cvb;

namespace CommonVisionNodes
{
    /// <summary>
    /// Applies binary thresholding to an image. Pixels at or above
    /// <see cref="Threshold"/> become white (255); all others become black (0).
    /// </summary>
    public sealed class BinarizeNode : Node
    {
        private Image? _lastResult;

        /// <summary>
        /// Input port that receives the source image.
        /// </summary>
        public Port ImageInput { get; }

        /// <summary>
        /// Output port that provides the binarized image.
        /// </summary>
        public Port ImageOutput { get; }

        /// <summary>
        /// Threshold value (0–255) used for binarization.
        /// </summary>
        public int Threshold { get; set; } = 128;

        public BinarizeNode()
        {
            ImageInput = AddInput("Image", typeof(Image), "The source image to binarize.");
            ImageOutput = AddOutput("Image", typeof(Image), "The binary image (black/white) after thresholding.");
        }

        /// <inheritdoc/>
        public override void Execute()
        {
            var source = (Image)ImageInput.Value!;
            _lastResult?.Dispose();
            _lastResult = new Image(source.Size, source.Planes.Count);
            byte threshold = (byte)Math.Clamp(Threshold, 0, 255);
            int width = source.Width;
            int height = source.Height;

            for (int p = 0; p < source.Planes.Count; p++)
            {
                var srcAccess = source.Planes[p].GetLinearAccess();
                var dstAccess = _lastResult.Planes[p].GetLinearAccess();

                unsafe
                {
                    byte* srcBase = (byte*)srcAccess.BasePtr;
                    byte* dstBase = (byte*)dstAccess.BasePtr;
                    long srcYInc = srcAccess.YInc;
                    long dstYInc = dstAccess.YInc;

                    if (srcAccess.XInc == 1 && dstAccess.XInc == 1)
                    {
                        var threshVec = Vector256.Create(threshold);
                        int vecLen = Vector256<byte>.Count;

                        for (int y = 0; y < height; y++)
                        {
                            byte* srcRow = srcBase + y * srcYInc;
                            byte* dstRow = dstBase + y * dstYInc;
                            int x = 0;

                            if (Vector256.IsHardwareAccelerated)
                            {
                                for (; x <= width - vecLen; x += vecLen)
                                {
                                    var v = Vector256.Load(srcRow + x);
                                    Vector256.GreaterThanOrEqual(v, threshVec).Store(dstRow + x);
                                }
                            }

                            for (; x < width; x++)
                                dstRow[x] = srcRow[x] >= threshold ? (byte)255 : (byte)0;
                        }
                    }
                    else
                    {
                        long srcXInc = srcAccess.XInc;
                        long dstXInc = dstAccess.XInc;

                        for (int y = 0; y < height; y++)
                        {
                            byte* srcRow = srcBase + y * srcYInc;
                            byte* dstRow = dstBase + y * dstYInc;
                            for (int x = 0; x < width; x++)
                            {
                                byte val = *(srcRow + x * srcXInc);
                                *(dstRow + x * dstXInc) = val >= threshold ? (byte)255 : (byte)0;
                            }
                        }
                    }
                }
            }

            ImageOutput.Value = _lastResult;
        }

        // Code generation

        /// <inheritdoc/>
        public override string CodeVariableName => "binarized";

        /// <inheritdoc/>
        public override IReadOnlyList<string> RequiredUsings => ["System.Runtime.InteropServices"];

        /// <inheritdoc/>
        public override void EmitCode(CodeEmitContext context)
        {
            var inputVar = context.ResolveInput(ImageInput);
            if (inputVar == null) return;

            var varName = context.GetUniqueVariable(CodeVariableName);
            context.Builder.AppendLine($"// Binarize (threshold: {Threshold})");
            context.Builder.AppendLine($"using var {varName} = Binarize({inputVar}, {Threshold});");
            context.RegisterOutput(ImageOutput, varName);
        }

        /// <inheritdoc/>
        public override void EmitHelperMethods(StringBuilder sb)
        {
            sb.AppendLine("static Image Binarize(Image source, int threshold)");
            sb.AppendLine("{");
            sb.AppendLine("    var result = new Image(source.Size, source.Planes.Count);");
            sb.AppendLine("    for (int p = 0; p < source.Planes.Count; p++)");
            sb.AppendLine("    {");
            sb.AppendLine("        var srcAccess = source.Planes[p].GetLinearAccess();");
            sb.AppendLine("        var dstAccess = result.Planes[p].GetLinearAccess();");
            sb.AppendLine("        for (int y = 0; y < source.Height; y++)");
            sb.AppendLine("        {");
            sb.AppendLine("            for (int x = 0; x < source.Width; x++)");
            sb.AppendLine("            {");
            sb.AppendLine("                var srcPtr = srcAccess.BasePtr + (nint)(y * srcAccess.YInc + x * srcAccess.XInc);");
            sb.AppendLine("                var dstPtr = dstAccess.BasePtr + (nint)(y * dstAccess.YInc + x * dstAccess.XInc);");
            sb.AppendLine("                byte val = Marshal.ReadByte(srcPtr);");
            sb.AppendLine("                Marshal.WriteByte(dstPtr, val >= threshold ? (byte)255 : (byte)0);");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("    return result;");
            sb.AppendLine("}");
        }
    }
}
