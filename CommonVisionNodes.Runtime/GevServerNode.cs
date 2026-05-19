using System.Net;
using System.Net.Sockets;
using Stemmer.Cvb;
using CvbGevServer = Stemmer.Cvb.GevServer.GevServer;
using DriverType = Stemmer.Cvb.GevServer.DriverType;

namespace CommonVisionNodes
{
    /// <summary>
    /// Streams incoming images through a Common Vision Blox GigE Vision Server.
    /// </summary>
    public sealed class GevServerNode : Node, IInitializable
    {
        private readonly record struct ServerImageFormat(
            int Width,
            int Height,
            ColorModel ColorModel,
            int DataTypeNativeDescriptor);

        private CvbGevServer? _server;
        private ServerImageFormat? _currentFormat;

        /// <summary>
        /// Input port that receives the image to stream.
        /// </summary>
        public Port ImageInput { get; }

        /// <summary>
        /// IPv4 address of the local network adapter the server binds to.
        /// </summary>
        public string LocalAddress { get; set; } = "127.0.0.1";

        /// <summary>
        /// GigE Vision driver used for communication and streaming.
        /// </summary>
        public DriverType DriverType { get; set; } = DriverType.Socket;

        /// <summary>
        /// Number of full-frame resend buffers to keep for packet resend.
        /// </summary>
        public int ResendBuffersCount { get; set; }

        /// <summary>
        /// Most recent send or server state message.
        /// </summary>
        public string LastStatus { get; private set; } = "Stopped.";

        /// <summary>
        /// Number of frames accepted by the server stream.
        /// </summary>
        public long SentFrameCount { get; private set; }

        /// <summary>
        /// Number of frames not accepted by the server stream.
        /// </summary>
        public long DroppedFrameCount { get; private set; }

        /// <inheritdoc/>
        public bool IsInitialized { get; private set; }

        /// <summary>
        /// Creates a GigE Vision server node with one image input.
        /// </summary>
        public GevServerNode()
        {
            ImageInput = AddInput("Image", typeof(Image), "The image to stream through the GigE Vision server.");
        }

        /// <inheritdoc/>
        public void Initialize()
        {
            _ = ParseLocalAddress();
            IsInitialized = true;
            LastStatus = "Waiting for first image.";
        }

        /// <inheritdoc/>
        public override void Execute()
        {
            if (!IsInitialized)
                throw new InvalidOperationException($"{nameof(GevServerNode)} must be initialized before execution.");

            var image = ImageInput.Value as Image;
            if (image is null)
            {
                LastStatus = "No image connected.";
                return;
            }

            if (image.IsDisposed)
                throw new InvalidOperationException("Cannot stream a disposed image.");

            EnsureServer(image);

            var stream = _server!.Stream;
            if (stream is null)
                throw new InvalidOperationException("The GigE Vision server was not created with a stream.");

            if (!stream.IsRunning)
            {
                LastStatus = $"Listening on {LocalAddress}; waiting for acquisition.";
                return;
            }

            if (stream.TrySend(image))
            {
                SentFrameCount++;
                LastStatus = $"Sent {SentFrameCount} frame(s).";
            }
            else
            {
                DroppedFrameCount++;
                LastStatus = $"Frame not accepted ({DroppedFrameCount} dropped).";
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            DisposeServer();
            IsInitialized = false;
            LastStatus = "Stopped.";
        }

        private void EnsureServer(Image image)
        {
            if (image.Planes.Count == 0)
                throw new InvalidOperationException("Cannot stream an image without planes.");

            var dataType = image.Planes[0].DataType;
            var format = new ServerImageFormat(
                image.Width,
                image.Height,
                image.ColorModel,
                dataType.NativeDescriptor);

            if (_server is not null && _currentFormat == format)
                return;

            DisposeServer();

            // CVB servers are created for a fixed image format. Recreate when the upstream
            // image dimensions, color model, or data type changes.
            var server = CvbGevServer.CreateWithConstSize(image.Size, image.ColorModel, dataType, DriverType);
            try
            {
                server.Stream.ResendBuffersCount = Math.Max(0, ResendBuffersCount);
                server.Start(ParseLocalAddress());

                _server = server;
                _currentFormat = format;
                LastStatus = $"Listening on {LocalAddress}.";
            }
            catch
            {
                server.Dispose();
                throw;
            }
        }

        private IPAddress ParseLocalAddress()
        {
            if (!IPAddress.TryParse(LocalAddress, out var address) || address.AddressFamily != AddressFamily.InterNetwork)
                throw new InvalidOperationException("GevServer local address must be a valid IPv4 address.");

            return address;
        }

        private void DisposeServer()
        {
            var server = _server;
            _server = null;
            _currentFormat = null;

            if (server is null)
                return;

            try
            {
                server.Stop();
            }
            catch
            {
                // Stop can fail if the native server is already unwound.
            }

            server.Dispose();
        }

        // Code generation

        /// <inheritdoc/>
        public override IReadOnlyList<string> RequiredUsings => ["Stemmer.Cvb.GevServer", "System.Net"];

        /// <inheritdoc/>
        public override void EmitCode(CodeEmitContext context)
        {
            var inputVar = context.ResolveInput(ImageInput);
            if (inputVar == null) return;

            var serverVar = context.GetUniqueVariable("gevServer");
            var streamVar = context.GetUniqueVariable("gevStream");
            var address = CodeEmitContext.EscapeVerbatim(LocalAddress);

            context.Builder.AppendLine("// Stream image through GigE Vision Server");
            context.Builder.AppendLine($"using var {serverVar} = GevServer.CreateWithConstSize({inputVar}.Size, {inputVar}.ColorModel, {inputVar}.Planes[0].DataType, DriverType.{DriverType});");
            context.Builder.AppendLine($"{serverVar}.Stream.ResendBuffersCount = {Math.Max(0, ResendBuffersCount)};");
            context.Builder.AppendLine($"{serverVar}.Start(IPAddress.Parse(@\"{address}\"));");
            context.Builder.AppendLine($"var {streamVar} = {serverVar}.Stream;");
            context.Builder.AppendLine($"if ({streamVar}.IsRunning)");
            context.Builder.AppendLine($"    _ = {streamVar}.TrySend({inputVar});");
            context.Builder.AppendLine($"{serverVar}.Stop();");
        }
    }
}
