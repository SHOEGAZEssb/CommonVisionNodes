using System.Runtime.InteropServices;
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
    }
}
