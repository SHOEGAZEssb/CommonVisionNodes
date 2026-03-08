using System.Runtime.InteropServices;
using System.Text;
using Stemmer.Cvb;

namespace CommonVisionNodes
{
    public sealed class SubImageNode : Node
    {
        private Image? _lastResult;

        public Port ImageInput { get; }
        public Port ImageOutput { get; }

        public int AreaX { get; set; }
        public int AreaY { get; set; }
        public int AreaWidth { get; set; } = 64;
        public int AreaHeight { get; set; } = 64;

        public SubImageNode()
        {
            ImageInput = AddInput("Image", typeof(Image));
            ImageOutput = AddOutput("Image", typeof(Image));
        }

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

                for (int dy = 0; dy < h; dy++)
                {
                    for (int dx = 0; dx < w; dx++)
                    {
                        var srcPtr = srcAccess.BasePtr + (nint)((y + dy) * srcAccess.YInc + (x + dx) * srcAccess.XInc);
                        var dstPtr = dstAccess.BasePtr + (nint)(dy * dstAccess.YInc + dx * dstAccess.XInc);
                        Marshal.WriteByte(dstPtr, Marshal.ReadByte(srcPtr));
                    }
                }
            }

            ImageOutput.Value = _lastResult;
        }

        // Code generation

        public override string CodeVariableName => "cropped";

        public override IReadOnlyList<string> RequiredUsings => ["System.Runtime.InteropServices"];

        public override void EmitCode(CodeEmitContext context)
        {
            var inputVar = context.ResolveInput(ImageInput);
            if (inputVar == null) return;

            var varName = context.GetUniqueVariable(CodeVariableName);
            context.Builder.AppendLine($"// Crop image (x: {AreaX}, y: {AreaY}, w: {AreaWidth}, h: {AreaHeight})");
            context.Builder.AppendLine($"using var {varName} = Crop({inputVar}, {AreaX}, {AreaY}, {AreaWidth}, {AreaHeight});");
            context.RegisterOutput(ImageOutput, varName);
        }

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
