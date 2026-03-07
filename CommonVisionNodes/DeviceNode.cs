using Stemmer.Cvb;
using Stemmer.Cvb.Driver;

namespace CommonVisionNodes
{
    public sealed class DeviceNode : Node, IInitializable
    {
        private GenICamDevice? _device;
        private ImageStream? _stream;
        private Image? _lastAcquiredImage;

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

            using var streamImage = _stream!.WaitFor(TimeSpan.FromSeconds(3));
            _lastAcquiredImage?.Dispose();
            _lastAcquiredImage = streamImage.Clone();
            ImageOutput.Value = _lastAcquiredImage;
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

            _lastAcquiredImage?.Dispose();
            _lastAcquiredImage = null;

            IsInitialized = false;
        }
    }
}
