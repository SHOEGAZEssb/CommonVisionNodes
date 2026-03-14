using System.Runtime.Intrinsics;
using System.Text;
using Stemmer.Cvb;

namespace CommonVisionNodes
{
    /// <summary>
    /// Applies min-max normalization (histogram stretching) to an image.
    /// Linearly maps pixel values from the source range to
    /// [<see cref="OutputMin"/>, <see cref="OutputMax"/>], providing
    /// brightness and contrast control.
    /// </summary>
    public sealed class NormalizeNode : Node
    {
        private Image? _lastResult;

        /// <summary>
        /// Input port that receives the source image.
        /// </summary>
        public Port ImageInput { get; }

        /// <summary>
        /// Output port that provides the normalized image.
        /// </summary>
        public Port ImageOutput { get; }

        /// <summary>
        /// Lower bound of the output range (0–255).
        /// </summary>
        public int OutputMin { get; set; } = 0;

        /// <summary>
        /// Upper bound of the output range (0–255).
        /// </summary>
        public int OutputMax { get; set; } = 255;

        public NormalizeNode()
        {
            ImageInput = AddInput("Image", typeof(Image), "The source image to normalize.");
            ImageOutput = AddOutput("Image", typeof(Image), "The normalized image.");
        }

        /// <inheritdoc/>
        public override void Execute()
        {
            var source = (Image)ImageInput.Value!;
            _lastResult?.Dispose();
            _lastResult = new Image(source.Size, source.Planes.Count);
            byte outMin = (byte)Math.Clamp(OutputMin, 0, 255);
            byte outMax = (byte)Math.Clamp(OutputMax, 0, 255);
            int width = source.Width;
            int height = source.Height;

            for (int p = 0; p < source.Planes.Count; p++)
            {
                var srcAccess = source.Planes[p].GetLinearAccess();
                var dstAccess = _lastResult.Planes[p].GetLinearAccess();

                // First pass: find source min/max
                byte srcMin = 255;
                byte srcMax = 0;

                unsafe
                {
                    byte* srcBase = (byte*)srcAccess.BasePtr;
                    long srcYInc = srcAccess.YInc;
                    long srcXInc = srcAccess.XInc;

                    for (int y = 0; y < height; y++)
                    {
                        byte* srcRow = srcBase + y * srcYInc;
                        for (int x = 0; x < width; x++)
                        {
                            byte val = *(srcRow + x * srcXInc);
                            if (val < srcMin) srcMin = val;
                            if (val > srcMax) srcMax = val;
                        }
                    }

                    // Build lookup table
                    byte* lut = stackalloc byte[256];
                    int srcRange = srcMax - srcMin;
                    int outRange = outMax - outMin;

                    if (srcRange == 0)
                    {
                        for (int i = 0; i < 256; i++)
                            lut[i] = outMin;
                    }
                    else
                    {
                        for (int i = 0; i < 256; i++)
                        {
                            int clamped = Math.Clamp(i, srcMin, srcMax);
                            lut[i] = (byte)Math.Clamp((clamped - srcMin) * outRange / srcRange + outMin, 0, 255);
                        }
                    }

                    // Second pass: apply LUT
                    byte* dstBase = (byte*)dstAccess.BasePtr;
                    long dstYInc = dstAccess.YInc;

                    if (srcAccess.XInc == 1 && dstAccess.XInc == 1)
                    {
                        for (int y = 0; y < height; y++)
                        {
                            byte* srcRow = srcBase + y * srcYInc;
                            byte* dstRow = dstBase + y * dstYInc;
                            for (int x = 0; x < width; x++)
                                dstRow[x] = lut[srcRow[x]];
                        }
                    }
                    else
                    {
                        long dstXInc = dstAccess.XInc;

                        for (int y = 0; y < height; y++)
                        {
                            byte* srcRow = srcBase + y * srcYInc;
                            byte* dstRow = dstBase + y * dstYInc;
                            for (int x = 0; x < width; x++)
                            {
                                byte val = *(srcRow + x * srcXInc);
                                *(dstRow + x * dstXInc) = lut[val];
                            }
                        }
                    }
                }
            }

            ImageOutput.Value = _lastResult;
        }

        // Code generation

        /// <inheritdoc/>
        public override string CodeVariableName => "normalized";

        /// <inheritdoc/>
        public override IReadOnlyList<string> RequiredUsings => ["System.Runtime.InteropServices"];

        /// <inheritdoc/>
        public override void EmitCode(CodeEmitContext context)
        {
            var inputVar = context.ResolveInput(ImageInput);
            if (inputVar == null) return;

            var varName = context.GetUniqueVariable(CodeVariableName);
            context.Builder.AppendLine($"// Normalize (output range: {OutputMin}–{OutputMax})");
            context.Builder.AppendLine($"using var {varName} = Normalize({inputVar}, {OutputMin}, {OutputMax});");
            context.RegisterOutput(ImageOutput, varName);
        }

        /// <inheritdoc/>
        public override void EmitHelperMethods(StringBuilder sb)
        {
            sb.AppendLine("static Image Normalize(Image source, int outMin, int outMax)");
            sb.AppendLine("{");
            sb.AppendLine("    var result = new Image(source.Size, source.Planes.Count);");
            sb.AppendLine("    for (int p = 0; p < source.Planes.Count; p++)");
            sb.AppendLine("    {");
            sb.AppendLine("        var srcAccess = source.Planes[p].GetLinearAccess();");
            sb.AppendLine("        var dstAccess = result.Planes[p].GetLinearAccess();");
            sb.AppendLine("        byte srcMin = 255, srcMax = 0;");
            sb.AppendLine("        for (int y = 0; y < source.Height; y++)");
            sb.AppendLine("        {");
            sb.AppendLine("            for (int x = 0; x < source.Width; x++)");
            sb.AppendLine("            {");
            sb.AppendLine("                var ptr = srcAccess.BasePtr + (nint)(y * srcAccess.YInc + x * srcAccess.XInc);");
            sb.AppendLine("                byte val = Marshal.ReadByte(ptr);");
            sb.AppendLine("                if (val < srcMin) srcMin = val;");
            sb.AppendLine("                if (val > srcMax) srcMax = val;");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine("        int srcRange = srcMax - srcMin;");
            sb.AppendLine("        int outRange = outMax - outMin;");
            sb.AppendLine("        for (int y = 0; y < source.Height; y++)");
            sb.AppendLine("        {");
            sb.AppendLine("            for (int x = 0; x < source.Width; x++)");
            sb.AppendLine("            {");
            sb.AppendLine("                var srcPtr = srcAccess.BasePtr + (nint)(y * srcAccess.YInc + x * srcAccess.XInc);");
            sb.AppendLine("                var dstPtr = dstAccess.BasePtr + (nint)(y * dstAccess.YInc + x * dstAccess.XInc);");
            sb.AppendLine("                byte val = Marshal.ReadByte(srcPtr);");
            sb.AppendLine("                byte mapped = srcRange == 0 ? (byte)outMin : (byte)Math.Clamp((val - srcMin) * outRange / srcRange + outMin, 0, 255);");
            sb.AppendLine("                Marshal.WriteByte(dstPtr, mapped);");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("    return result;");
            sb.AppendLine("}");
        }
    }
}
