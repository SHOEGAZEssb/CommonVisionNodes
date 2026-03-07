using System.Runtime.InteropServices;
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
    }
}
