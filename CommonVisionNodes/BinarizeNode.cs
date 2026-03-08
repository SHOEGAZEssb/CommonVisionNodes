using System.Runtime.InteropServices;
using System.Text;
using Stemmer.Cvb;

namespace CommonVisionNodes
{
    public sealed class BinarizeNode : Node
    {
        private Image? _lastResult;

        public Port ImageInput { get; }
        public Port ImageOutput { get; }

        public int Threshold { get; set; } = 128;

        public BinarizeNode()
        {
            ImageInput = AddInput("Image", typeof(Image));
            ImageOutput = AddOutput("Image", typeof(Image));
        }

        public override void Execute()
        {
            var source = (Image)ImageInput.Value!;
            _lastResult?.Dispose();
            _lastResult = new Image(source.Size, source.Planes.Count);

            for (int p = 0; p < source.Planes.Count; p++)
            {
                var srcAccess = source.Planes[p].GetLinearAccess();
                var dstAccess = _lastResult.Planes[p].GetLinearAccess();

                for (int y = 0; y < source.Height; y++)
                {
                    for (int x = 0; x < source.Width; x++)
                    {
                        var srcPtr = srcAccess.BasePtr + (nint)(y * srcAccess.YInc + x * srcAccess.XInc);
                        var dstPtr = dstAccess.BasePtr + (nint)(y * dstAccess.YInc + x * dstAccess.XInc);
                        byte val = Marshal.ReadByte(srcPtr);
                        Marshal.WriteByte(dstPtr, val >= Threshold ? (byte)255 : (byte)0);
                    }
                }
            }

            ImageOutput.Value = _lastResult;
        }

        // Code generation

        public override string CodeVariableName => "binarized";

        public override IReadOnlyList<string> RequiredUsings => ["System.Runtime.InteropServices"];

        public override void EmitCode(CodeEmitContext context)
        {
            var inputVar = context.ResolveInput(ImageInput);
            if (inputVar == null) return;

            var varName = context.GetUniqueVariable(CodeVariableName);
            context.Builder.AppendLine($"// Binarize (threshold: {Threshold})");
            context.Builder.AppendLine($"using var {varName} = Binarize({inputVar}, {Threshold});");
            context.RegisterOutput(ImageOutput, varName);
        }

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
