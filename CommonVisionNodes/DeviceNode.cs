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

        // Code generation

        public override string CodeVariableName => "acquiredImage";

        public override IReadOnlyList<string> RequiredUsings => ["Stemmer.Cvb.Driver"];

        public override void EmitCode(CodeEmitContext context)
        {
            var deviceVar = context.GetUniqueVariable("device");
            var streamVar = context.GetUniqueVariable("stream");
            var waitVar = context.GetUniqueVariable("streamResult");
            var imageVar = context.GetUniqueVariable(CodeVariableName);

            var sb = context.Builder;
            sb.AppendLine("// Acquire image from device");
            sb.AppendLine($"using var {deviceVar} = DeviceFactory.Open(@\"{CodeEmitContext.EscapeVerbatim(AccessToken)}\", AcquisitionStack.GenTL) as GenICamDevice;");
            sb.AppendLine($"using var {streamVar} = {deviceVar}!.GetStream<ImageStream>(0);");
            sb.AppendLine($"{streamVar}.Start();");
            sb.AppendLine($"using var {waitVar} = {streamVar}.WaitFor(TimeSpan.FromSeconds(3));");
            sb.AppendLine($"using var {imageVar} = {waitVar}.Clone();");
            sb.AppendLine($"{streamVar}.TryStop();");
            context.RegisterOutput(ImageOutput, imageVar);
        }
    }
}
