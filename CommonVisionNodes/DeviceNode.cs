using Stemmer.Cvb;
using Stemmer.Cvb.Driver;
using Stemmer.Cvb.GenApi;

namespace CommonVisionNodes
{
    public sealed class DeviceNode : Node, IInitializable
    {
        private GenICamDevice? _device;
        private ImageStream? _stream;
        private Image? _lastAcquiredImage;

        public Port ImageOutput { get; }

        public string AccessToken { get; set; } = string.Empty;

        public string SerialNumber { get; private set; } = string.Empty;

        public bool IsInitialized { get; private set; }

        public DeviceNode()
        {
            ImageOutput = AddOutput("Image", typeof(Image));
        }

        public void Initialize()
        {
            Dispose();
            _device = DeviceFactory.Open(AccessToken, AcquisitionStack.GenTL) as GenICamDevice;

            if (_device?.NodeMaps[NodeMapNames.Device]["DeviceSerialNumber"] is StringNode serialNode)
                SerialNumber = serialNode.Value;

            _stream = _device!.GetStream<ImageStream>(0);
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

            SerialNumber = string.Empty;
            IsInitialized = false;
        }

        // Code generation

        public override string CodeVariableName => "acquiredImage";

        public override IReadOnlyList<string> RequiredUsings => ["Stemmer.Cvb.Driver", "System.Linq"];

        public override void EmitCode(CodeEmitContext context)
        {
            var discoveryVar = context.GetUniqueVariable("discoveredDevice");
            var deviceVar = context.GetUniqueVariable("device");
            var streamVar = context.GetUniqueVariable("stream");
            var waitVar = context.GetUniqueVariable("streamResult");
            var imageVar = context.GetUniqueVariable(CodeVariableName);

            var sb = context.Builder;
            sb.AppendLine("// Discover and open device by serial number");
            sb.AppendLine($"var {discoveryVar} = DeviceFactory.Discover(DiscoverFlags.IgnoreVins)");
            sb.AppendLine($"    .First(d => d.TryGetProperty(DiscoveryProperties.DeviceSerialNumber, out var s) && s == \"{SerialNumber}\");");
            sb.AppendLine($"using var {deviceVar} = DeviceFactory.Open({discoveryVar}.AccessToken, AcquisitionStack.GenTL) as GenICamDevice;");
            sb.AppendLine($"using var {streamVar} = {deviceVar}!.GetStream<ImageStream>(0);");
            sb.AppendLine($"{streamVar}.Start();");
            sb.AppendLine($"using var {waitVar} = {streamVar}.WaitFor(TimeSpan.FromSeconds(3));");
            sb.AppendLine($"using var {imageVar} = {waitVar}.Clone();");
            sb.AppendLine($"{streamVar}.TryStop();");
            context.RegisterOutput(ImageOutput, imageVar);
        }
    }
}
