using System.Text;
using Stemmer.Cvb;
using Stemmer.Cvb.Polimago;

namespace CommonVisionNodes
{
    /// <summary>
    /// Result of a Polimago classification for a single region.
    /// </summary>
    public sealed class PolimagoClassifyResultItem
    {
        /// <summary>
        /// Zero-based index of the blob that was classified, or -1 for whole-image classification.
        /// </summary>
        public int BlobIndex { get; init; }

        /// <summary>
        /// Predicted class name.
        /// </summary>
        public string ClassName { get; init; } = string.Empty;

        /// <summary>
        /// Classification quality (0.0–1.0).
        /// </summary>
        public double Quality { get; init; }

        /// <summary>
        /// X coordinate of the point that was classified.
        /// </summary>
        public double X { get; init; }

        /// <summary>
        /// Y coordinate of the point that was classified.
        /// </summary>
        public double Y { get; init; }
    }

    /// <summary>
    /// Classifies image regions using a Polimago <see cref="ClassificationPredictor"/>.
    /// When blobs are connected, each blob's centroid is classified individually.
    /// Otherwise the image center is classified.
    /// </summary>
    public sealed class PolimagoClassifyNode : Node, IInitializable
    {
        private ClassificationPredictor? _predictor;

        /// <summary>
        /// Input port that receives the source image.
        /// </summary>
        public Port ImageInput { get; }

        /// <summary>
        /// Optional input port that receives blob bounding rectangles from a <see cref="BlobNode"/>.
        /// When connected, each blob centroid is classified individually.
        /// </summary>
        public Port BlobsInput { get; }

        /// <summary>
        /// Output port that passes the source image through.
        /// </summary>
        public Port ImageOutput { get; }

        /// <summary>
        /// Output port that provides the classification results.
        /// </summary>
        public Port ResultsOutput { get; }

        /// <summary>
        /// Path to the Polimago classifier file (.clf).
        /// </summary>
        public string ClassifierPath { get; set; } = string.Empty;

        /// <summary>
        /// Minimum quality threshold (0.0–1.0). Results below this value are discarded.
        /// </summary>
        public double MinQuality { get; set; } = 0.5;

        /// <summary>
        /// Number of classifications produced by the last execution.
        /// </summary>
        public int ResultCount { get; private set; }

        /// <summary>
        /// Detailed results from the last execution.
        /// </summary>
        public IReadOnlyList<PolimagoClassifyResultItem> Results { get; private set; } = [];

        /// <inheritdoc/>
        public bool IsInitialized { get; private set; }

        public PolimagoClassifyNode()
        {
            ImageInput = AddInput("Image", typeof(Image), "The source image to classify.");
            BlobsInput = AddInput("Blobs", typeof(IReadOnlyList<BlobRect>), "Optional blob bounding rectangles. When connected, each blob centroid is classified.");
            ImageOutput = AddOutput("Image", typeof(Image), "The source image passed through unchanged.");
            ResultsOutput = AddOutput("Results", typeof(IReadOnlyList<PolimagoClassifyResultItem>), "Classification results for each region.");
        }

        /// <inheritdoc/>
        public void Initialize()
        {
            _predictor?.Dispose();
            _predictor = new ClassificationPredictor(ClassifierPath);
            IsInitialized = true;
        }

        /// <inheritdoc/>
        public override void Execute()
        {
            if (!IsInitialized || _predictor is null)
                throw new InvalidOperationException($"{nameof(PolimagoClassifyNode)} must be initialized before execution.");

            var source = (Image)ImageInput.Value!;
            var results = new List<PolimagoClassifyResultItem>();

            if (BlobsInput.Value is IReadOnlyList<BlobRect> blobs && blobs.Count > 0)
            {
                for (int i = 0; i < blobs.Count; i++)
                {
                    var blob = blobs[i];
                    double cx = blob.X + blob.Width / 2.0;
                    double cy = blob.Y + blob.Height / 2.0;
                    var point = new Point2D((int)cx, (int)cy);

                    if (!_predictor.IsCompatible(source, point))
                        continue;

                    var result = _predictor.Classify(source, point);
                    if (result.Quality >= MinQuality)
                    {
                        results.Add(new PolimagoClassifyResultItem
                        {
                            BlobIndex = i,
                            ClassName = result.Name,
                            Quality = result.Quality,
                            X = cx,
                            Y = cy
                        });
                    }
                }
            }
            else
            {
                double cx = source.Width / 2.0;
                double cy = source.Height / 2.0;
                var point = new Point2D((int)cx, (int)cy);

                if (_predictor.IsCompatible(source, point))
                {
                    var result = _predictor.Classify(source, point);
                    if (result.Quality >= MinQuality)
                    {
                        results.Add(new PolimagoClassifyResultItem
                        {
                            BlobIndex = -1,
                            ClassName = result.Name,
                            Quality = result.Quality,
                            X = cx,
                            Y = cy
                        });
                    }
                }
            }

            Results = results;
            ResultCount = results.Count;
            ImageOutput.Value = source;
            ResultsOutput.Value = results;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            _predictor?.Dispose();
            _predictor = null;
            IsInitialized = false;
        }

        // Code generation

        /// <inheritdoc/>
        public override string CodeVariableName => "classified";

        /// <inheritdoc/>
        public override IReadOnlyList<string> RequiredUsings => ["Stemmer.Cvb.Polimago"];

        /// <inheritdoc/>
        public override void EmitCode(CodeEmitContext context)
        {
            var inputVar = context.ResolveInput(ImageInput);
            if (inputVar == null) return;

            var blobsVar = context.ResolveInput(BlobsInput);
            var predictorVar = context.GetUniqueVariable("classifier");
            var resultsVar = context.GetUniqueVariable(CodeVariableName);

            context.Builder.AppendLine($"// Polimago classification (min quality: {CodeEmitContext.FormatDouble(MinQuality)})");
            context.Builder.AppendLine($"using var {predictorVar} = new ClassificationPredictor(@\"{CodeEmitContext.EscapeVerbatim(ClassifierPath)}\");");

            if (blobsVar != null)
            {
                context.Builder.AppendLine($"var {resultsVar} = new List<(int BlobIndex, string ClassName, double Quality, double X, double Y)>();");
                context.Builder.AppendLine($"for (int i = 0; i < {blobsVar}.Count; i++)");
                context.Builder.AppendLine("{");
                context.Builder.AppendLine($"    var blob = {blobsVar}[i];");
                context.Builder.AppendLine("    var center = new Point2D((int)(blob.X + blob.Width / 2.0), (int)(blob.Y + blob.Height / 2.0));");
                context.Builder.AppendLine($"    if (!{predictorVar}.IsCompatible({inputVar}, center)) continue;");
                context.Builder.AppendLine($"    var result = {predictorVar}.Classify({inputVar}, center);");
                context.Builder.AppendLine($"    if (result.Quality >= {CodeEmitContext.FormatDouble(MinQuality)})");
                context.Builder.AppendLine($"        {resultsVar}.Add((i, result.Name, result.Quality, center.X, center.Y));");
                context.Builder.AppendLine("}");
            }
            else
            {
                context.Builder.AppendLine($"var center = new Point2D({inputVar}.Width / 2, {inputVar}.Height / 2);");
                context.Builder.AppendLine($"var {resultsVar} = new List<(string ClassName, double Quality)>();");
                context.Builder.AppendLine($"if ({predictorVar}.IsCompatible({inputVar}, center))");
                context.Builder.AppendLine("{");
                context.Builder.AppendLine($"    var result = {predictorVar}.Classify({inputVar}, center);");
                context.Builder.AppendLine($"    if (result.Quality >= {CodeEmitContext.FormatDouble(MinQuality)})");
                context.Builder.AppendLine($"        {resultsVar}.Add((result.Name, result.Quality));");
                context.Builder.AppendLine("}");
            }

            context.RegisterOutput(ImageOutput, inputVar);
            context.RegisterOutput(ResultsOutput, resultsVar);
        }
    }
}
