using Stemmer.Cvb;
using Stemmer.Cvb.Driver;

namespace CommonVisionNodes
{
    public sealed class DeviceNode : Node, IInitializable
    {
        private GenICamDevice? _device;
        private ImageStream? _stream;

        public Port ImageOutput { get; }

        public string AccessToken { get; set; } = string.Empty;

        public bool IsInitialized { get; private set; }

        public DeviceNode()
        {
            ImageOutput = AddOutput("Image", typeof(Image));
        }

        public void Initialize()
        {
            Dispose();
            _device = DeviceFactory.Open(AccessToken, AcquisitionStack.GenTL) as GenICamDevice;
            _stream = _device.GetStream<ImageStream>(0);
            _stream.Start();
            IsInitialized = true;
        }

        public override void Execute()
        {
            if (!IsInitialized)
                throw new InvalidOperationException($"{nameof(DeviceNode)} must be initialized before execution.");

            ImageOutput.Value = _stream!.WaitFor(TimeSpan.FromSeconds(3));
        }

        public void Dispose()
        {
            if (_stream != null)
            {
                _stream.TryStop();
                _stream = null;
            }

            _device?.Dispose();
            _device = null;
            IsInitialized = false;
        }
    }
}
