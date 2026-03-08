using System.Runtime.InteropServices;
using System.Text;
using Stemmer.Cvb;

namespace CommonVisionNodes
{
    /// <summary>
    /// Available test pattern types for <see cref="ImageGeneratorNode"/>.
    /// </summary>
    public enum TestPattern
    {
        /// <summary>
        /// Vertical gradient that scrolls horizontally.
        /// </summary>
        GradientH,

        /// <summary>
        /// Horizontal gradient that scrolls vertically.
        /// </summary>
        GradientV,

        /// <summary>
        /// Black-and-white checkerboard that shifts each frame.
        /// </summary>
        Checkerboard,

        /// <summary>
        /// Diagonal stripe pattern that moves each frame.
        /// </summary>
        Stripes,

        /// <summary>
        /// Concentric rings expanding outward.
        /// </summary>
        Rings
    }

    /// <summary>
    /// Generates synthetic test-pattern images that animate over successive executions.
    /// Does not require a camera or file — useful for testing pipelines.
    /// </summary>
    public sealed class ImageGeneratorNode : Node
    {
        private Image? _currentImage;
        private int _frameCounter;

        /// <summary>
        /// Output port that provides the generated image.
        /// </summary>
        public Port ImageOutput { get; }

        /// <summary>
        /// Width of the generated image in pixels.
        /// </summary>
        public int Width { get; set; } = 640;

        /// <summary>
        /// Height of the generated image in pixels.
        /// </summary>
        public int Height { get; set; } = 480;

        /// <summary>
        /// The test pattern to generate.
        /// </summary>
        public TestPattern Pattern { get; set; } = TestPattern.GradientH;

        /// <summary>
        /// Speed multiplier for the animation. Higher values move faster.
        /// </summary>
        public int Speed { get; set; } = 2;

        public ImageGeneratorNode()
        {
            ImageOutput = AddOutput("Image", typeof(Image));
        }

        /// <inheritdoc/>
        public override void Execute()
        {
            // Snapshot mutable properties so a UI-thread change mid-frame
            // cannot tear the image or cause out-of-bounds writes.
            int width = Width;
            int height = Height;
            var pattern = Pattern;
            int speed = Speed;
            int frame = _frameCounter;

            _currentImage?.Dispose();
            _currentImage = new Image(new Size2D(width, height), 1);

            var access = _currentImage.Planes[0].GetLinearAccess();
            double cx = width / 2.0;
            double cy = height / 2.0;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    byte val = pattern switch
                    {
                        TestPattern.GradientH => (byte)((x + frame * speed) % 256),
                        TestPattern.GradientV => (byte)((y + frame * speed) % 256),
                        TestPattern.Checkerboard => (byte)((((x + frame * speed) / 32) + ((y + frame * speed) / 32)) % 2 == 0 ? 255 : 0),
                        TestPattern.Stripes => (byte)((x + y + frame * speed) % 64 < 32 ? 255 : 0),
                        TestPattern.Rings => ComputeRings(x, y, cx, cy, frame, speed),
                        _ => 0
                    };

                    var ptr = access.BasePtr + (nint)(y * access.YInc + x * access.XInc);
                    Marshal.WriteByte(ptr, val);
                }
            }

            _frameCounter++;
            ImageOutput.Value = _currentImage;
        }

        private static byte ComputeRings(int x, int y, double cx, double cy, int frame, int speed)
        {
            double dx = x - cx;
            double dy = y - cy;
            double dist = Math.Sqrt(dx * dx + dy * dy);
            return (byte)(((int)(dist / 16.0) + frame * speed) % 2 == 0 ? 255 : 0);
        }

        // Code generation

        /// <inheritdoc/>
        public override string CodeVariableName => "generatedImage";

        /// <inheritdoc/>
        public override IReadOnlyList<string> RequiredUsings => ["System.Runtime.InteropServices"];

        /// <inheritdoc/>
        public override void EmitCode(CodeEmitContext context)
        {
            var varName = context.GetUniqueVariable(CodeVariableName);
            var sb = context.Builder;
            sb.AppendLine($"// Generate {Pattern} test pattern ({Width}x{Height})");
            sb.AppendLine($"using var {varName} = GenerateTestPattern({Width}, {Height});");
            context.RegisterOutput(ImageOutput, varName);
        }

        /// <inheritdoc/>
        public override void EmitHelperMethods(StringBuilder sb)
        {
            sb.AppendLine($"static Image GenerateTestPattern(int width, int height)");
            sb.AppendLine("{");
            sb.AppendLine("    var image = new Image(new Size2D(width, height), 1);");
            sb.AppendLine("    var access = image.Planes[0].GetLinearAccess();");
            sb.AppendLine("    for (int y = 0; y < height; y++)");
            sb.AppendLine("    {");
            sb.AppendLine("        for (int x = 0; x < width; x++)");
            sb.AppendLine("        {");
            sb.AppendLine("            byte val = (byte)((x + y) % 256);");
            sb.AppendLine("            var ptr = access.BasePtr + (nint)(y * access.YInc + x * access.XInc);");
            sb.AppendLine("            Marshal.WriteByte(ptr, val);");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("    return image;");
            sb.AppendLine("}");
        }
    }
}
