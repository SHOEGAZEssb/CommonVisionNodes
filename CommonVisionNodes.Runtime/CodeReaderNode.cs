using System.Globalization;
using System.Text;
using Stemmer.Cvb;
using Stemmer.Cvb.CodeReader;
using Stemmer.Cvb.CodeReader.Config;
using CodeReaderDecoder = Stemmer.Cvb.CodeReader.Decoder;

namespace CommonVisionNodes.Runtime
{
    /// <summary>
    /// Symbology presets exposed by <see cref="CodeReaderNode"/>.
    /// </summary>
    public enum CodeReaderSymbologySelection
    {
        /// <summary>
        /// Enables common production codes: Data Matrix, QR, Code 128, Code 39, EAN, and UPC.
        /// </summary>
        Common,

        /// <summary>
        /// Enables common 2D codes.
        /// </summary>
        TwoDimensional,

        /// <summary>
        /// Enables common 1D linear barcodes.
        /// </summary>
        Linear,

        /// <summary>
        /// Enables EAN, UPC, and GS1 DataBar codes.
        /// </summary>
        Retail,

        /// <summary>
        /// Enables postal symbologies.
        /// </summary>
        Postal,

        /// <summary>
        /// Enables every symbology exposed by the CVB CodeReader wrapper.
        /// </summary>
        All
    }

    /// <summary>
    /// Decoded barcode result produced by <see cref="CodeReaderNode"/>.
    /// </summary>
    public sealed class CodeReaderResultItem
    {
        /// <summary>
        /// One-based result index.
        /// </summary>
        public int Index { get; init; }

        /// <summary>
        /// Decoded payload.
        /// </summary>
        public string Data { get; init; } = string.Empty;

        /// <summary>
        /// CVB symbology name.
        /// </summary>
        public string Symbology { get; init; } = string.Empty;

        /// <summary>
        /// Decode status reported by CVB.
        /// </summary>
        public string DecodeStatus { get; init; } = string.Empty;

        /// <summary>
        /// X coordinate of the detected code center.
        /// </summary>
        public double CenterX { get; init; }

        /// <summary>
        /// Y coordinate of the detected code center.
        /// </summary>
        public double CenterY { get; init; }

        /// <summary>
        /// Four detected corner points in clockwise order.
        /// </summary>
        public IReadOnlyList<CodeReaderResultPoint> Corners { get; init; } = [];

        /// <summary>
        /// 2D result quality, when available.
        /// </summary>
        public int? Quality { get; init; }

        /// <summary>
        /// 2D row count, when available.
        /// </summary>
        public int? Rows { get; init; }

        /// <summary>
        /// 2D column count, when available.
        /// </summary>
        public int? Columns { get; init; }

        /// <summary>
        /// 2D symbol width, when available.
        /// </summary>
        public double? Width { get; init; }

        /// <summary>
        /// 2D symbol height, when available.
        /// </summary>
        public double? Height { get; init; }

        /// <inheritdoc/>
        public override string ToString()
            => $"{Index}: {Symbology} {Data} @ ({CenterX:F0},{CenterY:F0})";
    }

    /// <summary>
    /// One detected code corner.
    /// </summary>
    /// <param name="X">X coordinate in source image pixels.</param>
    /// <param name="Y">Y coordinate in source image pixels.</param>
    public readonly record struct CodeReaderResultPoint(double X, double Y);

    /// <summary>
    /// Reads barcodes from the first plane of a CVB image using the CVB CodeReader tool.
    /// </summary>
    public sealed class CodeReaderNode : Node, IInitializable
    {
        private CodeReaderDecoder? _decoder;
        private bool _configurationDirty = true;
        private CodeReaderSymbologySelection _symbologies = CodeReaderSymbologySelection.Common;
        private Polarity _codePolarity = Polarity.Either;
        private CodeSearchSpeed _codeSearchSpeed = CodeSearchSpeed.Speed0;
        private CustomPerformance _performanceMode = CustomPerformance.None;
        private int _detectorDensity = 3;
        private bool _basicInkjetDpmEnabled;

        /// <summary>
        /// Input port that receives the source image.
        /// </summary>
        public Port ImageInput { get; }

        /// <summary>
        /// Output port that passes the source image through.
        /// </summary>
        public Port ImageOutput { get; }

        /// <summary>
        /// Output port that provides detailed decoded code results.
        /// </summary>
        public Port ResultsOutput { get; }

        /// <summary>
        /// Output port that provides decoded payloads separated by newlines.
        /// </summary>
        public Port DataOutput { get; }

        /// <summary>
        /// Symbology preset to enable before decoding.
        /// </summary>
        public CodeReaderSymbologySelection Symbologies
        {
            get => _symbologies;
            set => SetConfigurationProperty(ref _symbologies, value);
        }

        /// <summary>
        /// Polarity used for Data Matrix and QR codes.
        /// </summary>
        public Polarity CodePolarity
        {
            get => _codePolarity;
            set => SetConfigurationProperty(ref _codePolarity, value);
        }

        /// <summary>
        /// Code search speed/robustness trade-off.
        /// </summary>
        public CodeSearchSpeed CodeSearchSpeed
        {
            get => _codeSearchSpeed;
            set => SetConfigurationProperty(ref _codeSearchSpeed, value);
        }

        /// <summary>
        /// Optional CodeReader performance mode.
        /// </summary>
        public CustomPerformance PerformanceMode
        {
            get => _performanceMode;
            set => SetConfigurationProperty(ref _performanceMode, value);
        }

        /// <summary>
        /// Detector density from 1 to 4. Lower values are more exhaustive for small codes.
        /// </summary>
        public int DetectorDensity
        {
            get => _detectorDensity;
            set => SetConfigurationProperty(ref _detectorDensity, Math.Clamp(value, 1, 4));
        }

        /// <summary>
        /// Enables the CVB Basic Inkjet DPM mode.
        /// </summary>
        public bool BasicInkjetDpmEnabled
        {
            get => _basicInkjetDpmEnabled;
            set => SetConfigurationProperty(ref _basicInkjetDpmEnabled, value);
        }

        /// <summary>
        /// Maximum number of codes to return. A value of 0 keeps the CVB default limit.
        /// </summary>
        public int MaxCodes { get; set; }

        /// <summary>
        /// Maximum decoding time in milliseconds. A value of 0 disables the time limit.
        /// </summary>
        public int TimeLimitMs { get; set; }

        /// <summary>
        /// Number of decoded codes from the last execution.
        /// </summary>
        public int ResultCount { get; private set; }

        /// <summary>
        /// Indicates whether the last time-limited decode hit the configured limit.
        /// </summary>
        public bool TimeLimitReached { get; private set; }

        /// <summary>
        /// Detailed decoded code results from the last execution.
        /// </summary>
        public IReadOnlyList<CodeReaderResultItem> Results { get; private set; } = [];

        /// <summary>
        /// Decoded payloads separated by newlines.
        /// </summary>
        public string DecodedData { get; private set; } = string.Empty;

        /// <inheritdoc/>
        public bool IsInitialized { get; private set; }

        /// <summary>
        /// Creates a CodeReader node with image input, image pass-through, result, and data outputs.
        /// </summary>
        public CodeReaderNode()
        {
            ImageInput = AddInput("Image", typeof(Image), "The source image containing one or more barcodes.");
            ImageOutput = AddOutput("Image", typeof(Image), "The source image passed through unchanged.");
            ResultsOutput = AddOutput("Results", typeof(IReadOnlyList<CodeReaderResultItem>), "Decoded barcode results with symbology and geometry.");
            DataOutput = AddOutput("Data", typeof(string), "Decoded payloads separated by newlines.");
        }

        /// <inheritdoc/>
        public void Initialize()
        {
            RecreateDecoder();
            IsInitialized = true;
        }

        /// <inheritdoc/>
        public override void Execute()
        {
            if (!IsInitialized)
                throw new InvalidOperationException($"{nameof(CodeReaderNode)} must be initialized before execution.");

            var source = (Image)ImageInput.Value!;
            if (source.Planes.Count == 0)
                throw new InvalidOperationException("CodeReader requires an image with at least one plane.");

            EnsureDecoder();

            var maxCodes = Math.Clamp(MaxCodes, 0, 256);
            IEnumerable<Result> rawResults;
            var timeLimitMs = Math.Clamp(TimeLimitMs, 0, 60000);
            if (timeLimitMs > 0)
            {
                var limitedResult = _decoder!.ExecuteFor(source.Planes[0], TimeSpan.FromMilliseconds(timeLimitMs), maxCodes);
                rawResults = limitedResult.ResultData;
                TimeLimitReached = limitedResult.Status == TimeLimitStatus.TimeLimitReached;
            }
            else
            {
                rawResults = _decoder!.Execute(source.Planes[0], maxCodes);
                TimeLimitReached = false;
            }

            var results = rawResults.Select(MapResult).ToList();
            Results = results;
            ResultCount = results.Count;
            DecodedData = string.Join(Environment.NewLine, results
                .Where(result => string.Equals(result.DecodeStatus, DecodeStatus.DecodeSuccess.ToString(), StringComparison.Ordinal))
                .Select(result => result.Data));

            ImageOutput.Value = source;
            ResultsOutput.Value = results;
            DataOutput.Value = DecodedData;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            _decoder?.Dispose();
            _decoder = null;
            IsInitialized = false;
            _configurationDirty = true;
        }

        // Code generation

        /// <inheritdoc/>
        public override string CodeVariableName => "codes";

        /// <inheritdoc/>
        public override IReadOnlyList<string> RequiredUsings =>
        [
            "Stemmer.Cvb.CodeReader",
            "Stemmer.Cvb.CodeReader.Config",
            "System.Linq"
        ];

        /// <inheritdoc/>
        public override void EmitCode(CodeEmitContext context)
        {
            var inputVar = context.ResolveInput(ImageInput);
            if (inputVar == null) return;

            var decoderVar = context.GetUniqueVariable("decoder");
            var resultsVar = context.GetUniqueVariable(CodeVariableName);
            var dataVar = context.GetUniqueVariable("decodedData");
            var maxCodes = Math.Clamp(MaxCodes, 0, 256);
            var timeLimitMs = Math.Clamp(TimeLimitMs, 0, 60000);

            var sb = context.Builder;
            sb.AppendLine($"// Decode barcodes ({Symbologies}, maxCodes: {maxCodes}, timeLimitMs: {timeLimitMs})");
            sb.AppendLine($"using var {decoderVar} = Decoder.Create();");
            sb.AppendLine($"{decoderVar}.CodeSearchSpeed = CodeSearchSpeed.{CodeSearchSpeed};");
            sb.AppendLine($"{decoderVar}.CustomPerformance = CustomPerformance.{PerformanceMode};");
            sb.AppendLine($"{decoderVar}.DetectorDensity = {DetectorDensity};");
            sb.AppendLine($"{decoderVar}.BasicInkjetDPMEnabled = {BasicInkjetDpmEnabled.ToString().ToLowerInvariant()};");
            EmitSymbologyConfiguration(sb, decoderVar, Symbologies, CodePolarity);

            if (timeLimitMs > 0)
            {
                var limitedVar = context.GetUniqueVariable("limitedDecode");
                sb.AppendLine($"var {limitedVar} = {decoderVar}.ExecuteFor({inputVar}.Planes[0], TimeSpan.FromMilliseconds({timeLimitMs}), {maxCodes});");
                sb.AppendLine($"var {resultsVar} = {limitedVar}.ResultData.ToList();");
                sb.AppendLine($"Console.WriteLine($\"CodeReader time limit reached: {{{limitedVar}.Status == TimeLimitStatus.TimeLimitReached}}\");");
            }
            else
            {
                sb.AppendLine($"var {resultsVar} = {decoderVar}.Execute({inputVar}.Planes[0], {maxCodes}).ToList();");
            }

            sb.AppendLine($"var {dataVar} = string.Join(Environment.NewLine, {resultsVar}.Where(r => r.DecodeStatus == DecodeStatus.DecodeSuccess).Select(r => r.Data));");
            sb.AppendLine($"Console.WriteLine($\"Codes found: {{{resultsVar}.Count}}\");");
            sb.AppendLine($"foreach (var code in {resultsVar})");
            sb.AppendLine("    Console.WriteLine($\"  {code.SymbolType}: {code.Data} @ ({code.Center.X},{code.Center.Y})\");");

            context.RegisterOutput(ImageOutput, inputVar);
            context.RegisterOutput(ResultsOutput, resultsVar);
            context.RegisterOutput(DataOutput, dataVar);
        }

        private void EnsureDecoder()
        {
            if (_decoder is null || _configurationDirty)
                RecreateDecoder();
        }

        private void RecreateDecoder()
        {
            _decoder?.Dispose();
            _decoder = CodeReaderDecoder.Create();
            _decoder.CodeSearchSpeed = CodeSearchSpeed;
            _decoder.CustomPerformance = PerformanceMode;
            _decoder.DetectorDensity = DetectorDensity;
            _decoder.BasicInkjetDPMEnabled = BasicInkjetDpmEnabled;
            ConfigureSymbologies(_decoder, Symbologies, CodePolarity);
            _configurationDirty = false;
        }

        private void SetConfigurationProperty<T>(ref T field, T value)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return;

            field = value;
            _configurationDirty = true;
        }

        private static CodeReaderResultItem MapResult(Result result, int index)
        {
            var result2D = result.Result2D;
            return new CodeReaderResultItem
            {
                Index = index + 1,
                Data = result.Data,
                Symbology = result.SymbolType.ToString(),
                DecodeStatus = result.DecodeStatus.ToString(),
                CenterX = result.Center.X,
                CenterY = result.Center.Y,
                Corners = [.. result.Corners.Select(corner => new CodeReaderResultPoint(corner.X, corner.Y))],
                Quality = result2D?.Quality,
                Rows = result2D?.Rows,
                Columns = result2D?.Columns,
                Width = result2D?.Size.Width,
                Height = result2D?.Size.Height
            };
        }

        internal static string FormatResultsForPreview(IReadOnlyList<CodeReaderResultItem> results, bool timeLimitReached)
        {
            if (results.Count == 0)
                return timeLimitReached ? "No codes found before time limit." : "No codes found.";

            var sb = new StringBuilder();
            foreach (var result in results)
            {
                if (sb.Length > 0)
                    sb.AppendLine();

                sb.Append(CultureInfo.InvariantCulture,
                    $"#{result.Index} {result.Symbology} {result.DecodeStatus} center=({result.CenterX:F0},{result.CenterY:F0})");

                if (result.Quality.HasValue)
                    sb.Append(CultureInfo.InvariantCulture, $" q={result.Quality.Value}");

                sb.Append(" data=");
                sb.Append(result.Data);
            }

            if (timeLimitReached)
            {
                sb.AppendLine();
                sb.Append("Time limit reached.");
            }

            return sb.ToString();
        }

        private static void ConfigureSymbologies(CodeReaderDecoder decoder, CodeReaderSymbologySelection selection, Polarity polarity)
        {
            switch (selection)
            {
                case CodeReaderSymbologySelection.Common:
                    EnableCommon(decoder, polarity);
                    break;
                case CodeReaderSymbologySelection.TwoDimensional:
                    EnableTwoDimensional(decoder, polarity);
                    break;
                case CodeReaderSymbologySelection.Linear:
                    EnableLinear(decoder);
                    break;
                case CodeReaderSymbologySelection.Retail:
                    EnableRetail(decoder);
                    break;
                case CodeReaderSymbologySelection.Postal:
                    EnablePostal(decoder);
                    break;
                case CodeReaderSymbologySelection.All:
                    EnableAll(decoder, polarity);
                    break;
                default:
                    EnableCommon(decoder, polarity);
                    break;
            }
        }

        private static void EmitSymbologyConfiguration(StringBuilder sb, string decoderVar, CodeReaderSymbologySelection selection, Polarity polarity)
        {
            switch (selection)
            {
                case CodeReaderSymbologySelection.Common:
                    EmitCommon(sb, decoderVar, polarity);
                    break;
                case CodeReaderSymbologySelection.TwoDimensional:
                    EmitTwoDimensional(sb, decoderVar, polarity);
                    break;
                case CodeReaderSymbologySelection.Linear:
                    EmitLinear(sb, decoderVar);
                    break;
                case CodeReaderSymbologySelection.Retail:
                    EmitRetail(sb, decoderVar);
                    break;
                case CodeReaderSymbologySelection.Postal:
                    EmitPostal(sb, decoderVar);
                    break;
                case CodeReaderSymbologySelection.All:
                    EmitAll(sb, decoderVar, polarity);
                    break;
                default:
                    EmitCommon(sb, decoderVar, polarity);
                    break;
            }
        }

        private static void EnableCommon(CodeReaderDecoder decoder, Polarity polarity)
        {
            EnableDataMatrixAndQr(decoder, polarity);
            decoder.GetConfig<Code128>().SetEnabled(true);
            decoder.GetConfig<Code39>().SetEnabled(true);
            decoder.GetConfig<Ean13>().SetEnabled(true);
            decoder.GetConfig<Ean8>().SetEnabled(true);
            decoder.GetConfig<UpcA>().SetEnabled(true);
            decoder.GetConfig<UpcE>().SetEnabled(true);
        }

        private static void EnableTwoDimensional(CodeReaderDecoder decoder, Polarity polarity)
        {
            EnableDataMatrixAndQr(decoder, polarity);
            decoder.GetConfig<Pdf417>().SetEnabled(true);
            decoder.GetConfig<MicroPdf417>().SetEnabled(true);
        }

        private static void EnableLinear(CodeReaderDecoder decoder)
        {
            decoder.GetConfig<Code11>().SetEnabled(true);
            decoder.GetConfig<Code128>().SetEnabled(true);
            decoder.GetConfig<Code39>().SetEnabled(true);
            decoder.GetConfig<Code93>().SetEnabled(true);
            decoder.GetConfig<Interleaved2of5>().SetEnabled(true);
            decoder.GetConfig<Pharmacode>().SetEnabled(true);
            decoder.GetConfig<Code32>().SetEnabled(true);
        }

        private static void EnableRetail(CodeReaderDecoder decoder)
        {
            decoder.GetConfig<Ean13>().SetEnabled(true);
            decoder.GetConfig<Ean8>().SetEnabled(true);
            decoder.GetConfig<UpcA>().SetEnabled(true);
            decoder.GetConfig<UpcE>().SetEnabled(true);
            decoder.GetConfig<GS1DataBar14>().SetEnabled(true);
            decoder.GetConfig<GS1DataBarStacked>().SetEnabled(true);
            decoder.GetConfig<GS1DataBarLimited>().SetEnabled(true);
            decoder.GetConfig<GS1DataBarExpanded>().SetEnabled(true);
            decoder.GetConfig<GS1DataBarExpandedStacked>().SetEnabled(true);
        }

        private static void EnablePostal(CodeReaderDecoder decoder)
        {
            decoder.GetConfig<AustraliaPost>().SetEnabled(true);
            decoder.GetConfig<DutchPost>().SetEnabled(true);
            decoder.GetConfig<RoyalMail>().SetEnabled(true);
            decoder.GetConfig<UspsIntelligentMail>().SetEnabled(true);
        }

        private static void EnableAll(CodeReaderDecoder decoder, Polarity polarity)
        {
            EnableTwoDimensional(decoder, polarity);
            EnableLinear(decoder);
            EnableRetail(decoder);
            EnablePostal(decoder);
        }

        private static void EnableDataMatrixAndQr(CodeReaderDecoder decoder, Polarity polarity)
        {
            decoder.GetConfig<DataMatrix>().SetEnabled(true).SetPolarity(polarity);
            decoder.GetConfig<QR>().SetEnabled(true).SetPolarity(polarity);
            decoder.GetConfig<MicroQR>().SetEnabled(true);
        }

        private static void EmitCommon(StringBuilder sb, string decoderVar, Polarity polarity)
        {
            EmitDataMatrixAndQr(sb, decoderVar, polarity);
            sb.AppendLine($"{decoderVar}.GetConfig<Code128>().SetEnabled(true);");
            sb.AppendLine($"{decoderVar}.GetConfig<Code39>().SetEnabled(true);");
            sb.AppendLine($"{decoderVar}.GetConfig<Ean13>().SetEnabled(true);");
            sb.AppendLine($"{decoderVar}.GetConfig<Ean8>().SetEnabled(true);");
            sb.AppendLine($"{decoderVar}.GetConfig<UpcA>().SetEnabled(true);");
            sb.AppendLine($"{decoderVar}.GetConfig<UpcE>().SetEnabled(true);");
        }

        private static void EmitTwoDimensional(StringBuilder sb, string decoderVar, Polarity polarity)
        {
            EmitDataMatrixAndQr(sb, decoderVar, polarity);
            sb.AppendLine($"{decoderVar}.GetConfig<Pdf417>().SetEnabled(true);");
            sb.AppendLine($"{decoderVar}.GetConfig<MicroPdf417>().SetEnabled(true);");
        }

        private static void EmitLinear(StringBuilder sb, string decoderVar)
        {
            sb.AppendLine($"{decoderVar}.GetConfig<Code11>().SetEnabled(true);");
            sb.AppendLine($"{decoderVar}.GetConfig<Code128>().SetEnabled(true);");
            sb.AppendLine($"{decoderVar}.GetConfig<Code39>().SetEnabled(true);");
            sb.AppendLine($"{decoderVar}.GetConfig<Code93>().SetEnabled(true);");
            sb.AppendLine($"{decoderVar}.GetConfig<Interleaved2of5>().SetEnabled(true);");
            sb.AppendLine($"{decoderVar}.GetConfig<Pharmacode>().SetEnabled(true);");
            sb.AppendLine($"{decoderVar}.GetConfig<Code32>().SetEnabled(true);");
        }

        private static void EmitRetail(StringBuilder sb, string decoderVar)
        {
            sb.AppendLine($"{decoderVar}.GetConfig<Ean13>().SetEnabled(true);");
            sb.AppendLine($"{decoderVar}.GetConfig<Ean8>().SetEnabled(true);");
            sb.AppendLine($"{decoderVar}.GetConfig<UpcA>().SetEnabled(true);");
            sb.AppendLine($"{decoderVar}.GetConfig<UpcE>().SetEnabled(true);");
            sb.AppendLine($"{decoderVar}.GetConfig<GS1DataBar14>().SetEnabled(true);");
            sb.AppendLine($"{decoderVar}.GetConfig<GS1DataBarStacked>().SetEnabled(true);");
            sb.AppendLine($"{decoderVar}.GetConfig<GS1DataBarLimited>().SetEnabled(true);");
            sb.AppendLine($"{decoderVar}.GetConfig<GS1DataBarExpanded>().SetEnabled(true);");
            sb.AppendLine($"{decoderVar}.GetConfig<GS1DataBarExpandedStacked>().SetEnabled(true);");
        }

        private static void EmitPostal(StringBuilder sb, string decoderVar)
        {
            sb.AppendLine($"{decoderVar}.GetConfig<AustraliaPost>().SetEnabled(true);");
            sb.AppendLine($"{decoderVar}.GetConfig<DutchPost>().SetEnabled(true);");
            sb.AppendLine($"{decoderVar}.GetConfig<RoyalMail>().SetEnabled(true);");
            sb.AppendLine($"{decoderVar}.GetConfig<UspsIntelligentMail>().SetEnabled(true);");
        }

        private static void EmitAll(StringBuilder sb, string decoderVar, Polarity polarity)
        {
            EmitTwoDimensional(sb, decoderVar, polarity);
            EmitLinear(sb, decoderVar);
            EmitRetail(sb, decoderVar);
            EmitPostal(sb, decoderVar);
        }

        private static void EmitDataMatrixAndQr(StringBuilder sb, string decoderVar, Polarity polarity)
        {
            sb.AppendLine($"{decoderVar}.GetConfig<DataMatrix>().SetEnabled(true).SetPolarity(Polarity.{polarity});");
            sb.AppendLine($"{decoderVar}.GetConfig<QR>().SetEnabled(true).SetPolarity(Polarity.{polarity});");
            sb.AppendLine($"{decoderVar}.GetConfig<MicroQR>().SetEnabled(true);");
        }
    }
}
