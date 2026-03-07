using System.Runtime.InteropServices;
using Stemmer.Cvb;

namespace CommonVisionNodes
{
    public sealed class MatrixTransformNode : Node
    {
        private Image? _lastResult;

        public Port ImageInput { get; }
        public Port ImageOutput { get; }

        public double Angle { get; set; }
        public double ScaleX { get; set; } = 1.0;
        public double ScaleY { get; set; } = 1.0;
        public double TranslateX { get; set; }
        public double TranslateY { get; set; }

        public MatrixTransformNode()
        {
            ImageInput = AddInput("Image", typeof(Image));
            ImageOutput = AddOutput("Image", typeof(Image));
        }

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

                for (int dy = 0; dy < srcH; dy++)
                {
                    for (int dx = 0; dx < srcW; dx++)
                    {
                        // Undo translate, move to center-relative coords
                        double rx = (dx - TranslateX - cx) * invSx;
                        double ry = (dy - TranslateY - cy) * invSy;

                        // Inverse rotation
                        double sx = rx * cos + ry * sin + cx;
                        double sy = -rx * sin + ry * cos + cy;

                        byte val = SampleBilinear(srcAccess, sx, sy, srcW, srcH);

                        var dstPtr = dstAccess.BasePtr + (nint)(dy * dstAccess.YInc + dx * dstAccess.XInc);
                        Marshal.WriteByte(dstPtr, val);
                    }
                }
            }

            ImageOutput.Value = _lastResult;
        }

        private static byte SampleBilinear(LinearAccessData access, double x, double y, int w, int h)
        {
            if (x < 0 || y < 0 || x >= w - 1 || y >= h - 1)
                return 0;

            int x0 = (int)x;
            int y0 = (int)y;
            int x1 = x0 + 1;
            int y1 = y0 + 1;

            double fx = x - x0;
            double fy = y - y0;

            byte v00 = Marshal.ReadByte(access.BasePtr + (nint)(y0 * access.YInc + x0 * access.XInc));
            byte v10 = Marshal.ReadByte(access.BasePtr + (nint)(y0 * access.YInc + x1 * access.XInc));
            byte v01 = Marshal.ReadByte(access.BasePtr + (nint)(y1 * access.YInc + x0 * access.XInc));
            byte v11 = Marshal.ReadByte(access.BasePtr + (nint)(y1 * access.YInc + x1 * access.XInc));

            double val = v00 * (1 - fx) * (1 - fy)
                       + v10 * fx * (1 - fy)
                       + v01 * (1 - fx) * fy
                       + v11 * fx * fy;

            return (byte)Math.Clamp(val, 0, 255);
        }
    }
}
