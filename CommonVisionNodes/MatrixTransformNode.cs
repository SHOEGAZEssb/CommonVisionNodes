using System.Text;
using Stemmer.Cvb;

namespace CommonVisionNodes
{
    /// <summary>
    /// Applies an affine transformation (rotation, scale, translation) to the input image
    /// using inverse mapping with bilinear interpolation.
    /// </summary>
    public sealed class MatrixTransformNode : Node
    {
        private Image? _lastResult;

        /// <summary>
        /// Input port that receives the source image.
        /// </summary>
        public Port ImageInput { get; }

        /// <summary>
        /// Output port that provides the transformed image.
        /// </summary>
        public Port ImageOutput { get; }

        /// <summary>
        /// Rotation angle in degrees.
        /// </summary>
        public double Angle { get; set; }

        /// <summary>
        /// Horizontal scale factor.
        /// </summary>
        public double ScaleX { get; set; } = 1.0;

        /// <summary>
        /// Vertical scale factor.
        /// </summary>
        public double ScaleY { get; set; } = 1.0;

        /// <summary>
        /// Horizontal translation in pixels.
        /// </summary>
        public double TranslateX { get; set; }

        /// <summary>
        /// Vertical translation in pixels.
        /// </summary>
        public double TranslateY { get; set; }

        public MatrixTransformNode()
        {
            ImageInput = AddInput("Image", typeof(Image), "The source image to transform.");
            ImageOutput = AddOutput("Image", typeof(Image), "The affine-transformed image.");
        }

        /// <inheritdoc/>
        public override void Execute()
        {
            var source = (Image)ImageInput.Value!;
            int srcW = source.Width;
            int srcH = source.Height;

            _lastResult?.Dispose();
            _lastResult = new Image(source.Size, source.Planes.Count);

            double rad = Angle * Math.PI / 180.0;
            double cos = Math.Cos(rad);
            double sin = Math.Sin(rad);

            double cx = srcW / 2.0;
            double cy = srcH / 2.0;

            // Inverse affine: for each destination pixel, find the source pixel.
            // Forward: rotate around center, scale, translate.
            // Inverse: undo translate, undo scale, undo rotation.
            double invSx = ScaleX == 0 ? 0 : 1.0 / ScaleX;
            double invSy = ScaleY == 0 ? 0 : 1.0 / ScaleY;

            for (int p = 0; p < source.Planes.Count; p++)
            {
                var srcAccess = source.Planes[p].GetLinearAccess();
                var dstAccess = _lastResult.Planes[p].GetLinearAccess();

                unsafe
                {
                    byte* dstBase = (byte*)dstAccess.BasePtr;
                    long dstYInc = dstAccess.YInc;
                    long dstXInc = dstAccess.XInc;

                    for (int dy = 0; dy < srcH; dy++)
                    {
                        byte* dstRow = dstBase + dy * dstYInc;
                        for (int dx = 0; dx < srcW; dx++)
                        {
                            // Undo translate, move to center-relative coords
                            double rx = (dx - TranslateX - cx) * invSx;
                            double ry = (dy - TranslateY - cy) * invSy;

                            // Inverse rotation
                            double sx = rx * cos + ry * sin + cx;
                            double sy = -rx * sin + ry * cos + cy;

                            *(dstRow + dx * dstXInc) = SampleBilinear(srcAccess, sx, sy, srcW, srcH);
                        }
                    }
                }
            }

            ImageOutput.Value = _lastResult;
        }

        /// <summary>
        /// Samples a pixel value at fractional coordinates using bilinear interpolation.
        /// </summary>
        /// <param name="access">Linear access data for the source plane.</param>
        /// <param name="x">Fractional x coordinate.</param>
        /// <param name="y">Fractional y coordinate.</param>
        /// <param name="w">Image width.</param>
        /// <param name="h">Image height.</param>
        /// <returns>Interpolated pixel value, or 0 if out of bounds.</returns>
        private static unsafe byte SampleBilinear(LinearAccessData access, double x, double y, int w, int h)
        {
            if (x < 0 || y < 0 || x >= w - 1 || y >= h - 1)
                return 0;

            int x0 = (int)x;
            int y0 = (int)y;
            int x1 = x0 + 1;
            int y1 = y0 + 1;

            double fx = x - x0;
            double fy = y - y0;

            byte* basePtr = (byte*)access.BasePtr;
            long yInc = access.YInc;
            long xInc = access.XInc;

            byte v00 = *(basePtr + y0 * yInc + x0 * xInc);
            byte v10 = *(basePtr + y0 * yInc + x1 * xInc);
            byte v01 = *(basePtr + y1 * yInc + x0 * xInc);
            byte v11 = *(basePtr + y1 * yInc + x1 * xInc);

            double val = v00 * (1 - fx) * (1 - fy)
                       + v10 * fx * (1 - fy)
                       + v01 * (1 - fx) * fy
                       + v11 * fx * fy;

            return (byte)Math.Clamp(val, 0, 255);
        }

        // Code generation

        /// <inheritdoc/>
        public override string CodeVariableName => "transformed";

        /// <inheritdoc/>
        public override IReadOnlyList<string> RequiredUsings => ["System.Runtime.InteropServices"];

        /// <inheritdoc/>
        public override void EmitCode(CodeEmitContext context)
        {
            var inputVar = context.ResolveInput(ImageInput);
            if (inputVar == null) return;

            var varName = context.GetUniqueVariable(CodeVariableName);
            context.Builder.AppendLine($"// Affine transform (angle: {CodeEmitContext.FormatDouble(Angle)}, scale: {CodeEmitContext.FormatDouble(ScaleX)}x{CodeEmitContext.FormatDouble(ScaleY)}, translate: {CodeEmitContext.FormatDouble(TranslateX)},{CodeEmitContext.FormatDouble(TranslateY)})");
            context.Builder.AppendLine($"using var {varName} = AffineTransform({inputVar}, {CodeEmitContext.FormatDouble(Angle)}, {CodeEmitContext.FormatDouble(ScaleX)}, {CodeEmitContext.FormatDouble(ScaleY)}, {CodeEmitContext.FormatDouble(TranslateX)}, {CodeEmitContext.FormatDouble(TranslateY)});");
            context.RegisterOutput(ImageOutput, varName);
        }

        /// <inheritdoc/>
        public override void EmitHelperMethods(StringBuilder sb)
        {
            sb.AppendLine("static Image AffineTransform(Image source, double angle, double scaleX, double scaleY, double translateX, double translateY)");
            sb.AppendLine("{");
            sb.AppendLine("    int srcW = source.Width;");
            sb.AppendLine("    int srcH = source.Height;");
            sb.AppendLine("    var result = new Image(source.Size, source.Planes.Count);");
            sb.AppendLine("    double rad = angle * Math.PI / 180.0;");
            sb.AppendLine("    double cos = Math.Cos(rad);");
            sb.AppendLine("    double sin = Math.Sin(rad);");
            sb.AppendLine("    double cx = srcW / 2.0;");
            sb.AppendLine("    double cy = srcH / 2.0;");
            sb.AppendLine("    double invSx = scaleX == 0 ? 0 : 1.0 / scaleX;");
            sb.AppendLine("    double invSy = scaleY == 0 ? 0 : 1.0 / scaleY;");
            sb.AppendLine("    for (int p = 0; p < source.Planes.Count; p++)");
            sb.AppendLine("    {");
            sb.AppendLine("        var srcAccess = source.Planes[p].GetLinearAccess();");
            sb.AppendLine("        var dstAccess = result.Planes[p].GetLinearAccess();");
            sb.AppendLine("        for (int dy = 0; dy < srcH; dy++)");
            sb.AppendLine("        {");
            sb.AppendLine("            for (int dx = 0; dx < srcW; dx++)");
            sb.AppendLine("            {");
            sb.AppendLine("                double rx = (dx - translateX - cx) * invSx;");
            sb.AppendLine("                double ry = (dy - translateY - cy) * invSy;");
            sb.AppendLine("                double sx = rx * cos + ry * sin + cx;");
            sb.AppendLine("                double sy = -rx * sin + ry * cos + cy;");
            sb.AppendLine("                byte val = SampleBilinear(srcAccess, sx, sy, srcW, srcH);");
            sb.AppendLine("                var dstPtr = dstAccess.BasePtr + (nint)(dy * dstAccess.YInc + dx * dstAccess.XInc);");
            sb.AppendLine("                Marshal.WriteByte(dstPtr, val);");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("    return result;");
            sb.AppendLine("}");
            sb.AppendLine();
            sb.AppendLine("static byte SampleBilinear(LinearAccessData access, double x, double y, int w, int h)");
            sb.AppendLine("{");
            sb.AppendLine("    if (x < 0 || y < 0 || x >= w - 1 || y >= h - 1)");
            sb.AppendLine("        return 0;");
            sb.AppendLine("    int x0 = (int)x;");
            sb.AppendLine("    int y0 = (int)y;");
            sb.AppendLine("    int x1 = x0 + 1;");
            sb.AppendLine("    int y1 = y0 + 1;");
            sb.AppendLine("    double fx = x - x0;");
            sb.AppendLine("    double fy = y - y0;");
            sb.AppendLine("    byte v00 = Marshal.ReadByte(access.BasePtr + (nint)(y0 * access.YInc + x0 * access.XInc));");
            sb.AppendLine("    byte v10 = Marshal.ReadByte(access.BasePtr + (nint)(y0 * access.YInc + x1 * access.XInc));");
            sb.AppendLine("    byte v01 = Marshal.ReadByte(access.BasePtr + (nint)(y1 * access.YInc + x0 * access.XInc));");
            sb.AppendLine("    byte v11 = Marshal.ReadByte(access.BasePtr + (nint)(y1 * access.YInc + x1 * access.XInc));");
            sb.AppendLine("    double val = v00 * (1 - fx) * (1 - fy)");
            sb.AppendLine("               + v10 * fx * (1 - fy)");
            sb.AppendLine("               + v01 * (1 - fx) * fy");
            sb.AppendLine("               + v11 * fx * fy;");
            sb.AppendLine("    return (byte)Math.Clamp(val, 0, 255);");
            sb.AppendLine("}");
        }
    }
}
