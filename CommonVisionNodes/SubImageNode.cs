using System.Text;
using Stemmer.Cvb;

namespace CommonVisionNodes
{
    /// <summary>
    /// Crops a rectangular region from the input image.
    /// </summary>
    public sealed class SubImageNode : Node
    {
        private Image? _lastResult;

        /// <summary>
        /// Input port that receives the source image.
        /// </summary>
        public Port ImageInput { get; }

        /// <summary>
        /// Output port that provides the cropped image.
        /// </summary>
        public Port ImageOutput { get; }

        /// <summary>
        /// X origin of the crop area in pixels.
        /// </summary>
        public int AreaX { get; set; }

        /// <summary>
        /// Y origin of the crop area in pixels.
        /// </summary>
        public int AreaY { get; set; }

        /// <summary>
        /// Width of the crop area in pixels.
        /// </summary>
        public int AreaWidth { get; set; } = 64;

        /// <summary>
        /// Height of the crop area in pixels.
        /// </summary>
        public int AreaHeight { get; set; } = 64;

        public SubImageNode()
        {
            ImageInput = AddInput("Image", typeof(Image), "The source image to crop.");
            ImageOutput = AddOutput("Image", typeof(Image), "The cropped sub-region of the input image.");
        }

        /// <inheritdoc/>
        public override void Execute()
        {
            var source = (Image)ImageInput.Value!;

            int x = Math.Clamp(AreaX, 0, Math.Max(0, source.Width - 1));
            int y = Math.Clamp(AreaY, 0, Math.Max(0, source.Height - 1));
            int w = Math.Clamp(AreaWidth, 1, source.Width - x);
            int h = Math.Clamp(AreaHeight, 1, source.Height - y);

            _lastResult?.Dispose();
            _lastResult = new Image(new Size2D(w, h), source.Planes.Count);

            for (int p = 0; p < source.Planes.Count; p++)
            {
                var srcAccess = source.Planes[p].GetLinearAccess();
                var dstAccess = _lastResult.Planes[p].GetLinearAccess();

                unsafe
                {
                    byte* srcBase = (byte*)srcAccess.BasePtr;
                    byte* dstBase = (byte*)dstAccess.BasePtr;
                    long srcYInc = srcAccess.YInc;
                    long srcXInc = srcAccess.XInc;
                    long dstYInc = dstAccess.YInc;
                    long dstXInc = dstAccess.XInc;

                    for (int dy = 0; dy < h; dy++)
                    {
                        byte* srcRow = srcBase + (y + dy) * srcYInc;
                        byte* dstRow = dstBase + dy * dstYInc;
                        for (int dx = 0; dx < w; dx++)
                        {
                            *(dstRow + dx * dstXInc) = *(srcRow + (x + dx) * srcXInc);
                        }
                    }
                }
            }

            ImageOutput.Value = _lastResult;
        }

        // Code generation

        /// <inheritdoc/>
        public override string CodeVariableName => "cropped";

        /// <inheritdoc/>
        public override IReadOnlyList<string> RequiredUsings => ["System.Runtime.InteropServices"];

        /// <inheritdoc/>
        public override void EmitCode(CodeEmitContext context)
        {
            var inputVar = context.ResolveInput(ImageInput);
            if (inputVar == null) return;

            var varName = context.GetUniqueVariable(CodeVariableName);
            context.Builder.AppendLine($"// Crop image (x: {AreaX}, y: {AreaY}, w: {AreaWidth}, h: {AreaHeight})");
            context.Builder.AppendLine($"using var {varName} = Crop({inputVar}, {AreaX}, {AreaY}, {AreaWidth}, {AreaHeight});");
            context.RegisterOutput(ImageOutput, varName);
        }

        /// <inheritdoc/>
        public override void EmitHelperMethods(StringBuilder sb)
        {
            sb.AppendLine("static Image Crop(Image source, int areaX, int areaY, int areaWidth, int areaHeight)");
            sb.AppendLine("{");
            sb.AppendLine("    int x = Math.Clamp(areaX, 0, Math.Max(0, source.Width - 1));");
            sb.AppendLine("    int y = Math.Clamp(areaY, 0, Math.Max(0, source.Height - 1));");
            sb.AppendLine("    int w = Math.Clamp(areaWidth, 1, source.Width - x);");
            sb.AppendLine("    int h = Math.Clamp(areaHeight, 1, source.Height - y);");
            sb.AppendLine("    var result = new Image(new Size2D(w, h), source.Planes.Count);");
            sb.AppendLine("    for (int p = 0; p < source.Planes.Count; p++)");
            sb.AppendLine("    {");
            sb.AppendLine("        var srcAccess = source.Planes[p].GetLinearAccess();");
            sb.AppendLine("        var dstAccess = result.Planes[p].GetLinearAccess();");
            sb.AppendLine("        for (int dy = 0; dy < h; dy++)");
            sb.AppendLine("        {");
            sb.AppendLine("            for (int dx = 0; dx < w; dx++)");
            sb.AppendLine("            {");
            sb.AppendLine("                var srcPtr = srcAccess.BasePtr + (nint)((y + dy) * srcAccess.YInc + (x + dx) * srcAccess.XInc);");
            sb.AppendLine("                var dstPtr = dstAccess.BasePtr + (nint)(dy * dstAccess.YInc + dx * dstAccess.XInc);");
            sb.AppendLine("                Marshal.WriteByte(dstPtr, Marshal.ReadByte(srcPtr));");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("    return result;");
            sb.AppendLine("}");
        }
    }
}
