using Stemmer.Cvb;
using Stemmer.Cvb.Minos;

namespace CommonVisionNodes.Runtime
{
    /// <summary>
    /// Search operation performed by a <see cref="MinosSearchNode"/>.
    /// </summary>
    public enum MinosSearchOperation
    {
        /// <summary>
        /// Find all matching patterns in the image.
        /// </summary>
        FindAll,

        /// <summary>
        /// Stop at the first matching pattern.
        /// </summary>
        FindFirst,

        /// <summary>
        /// Search the complete image and return the best matching pattern.
        /// </summary>
        FindBest,

        /// <summary>
        /// Search the complete image and return the best match with subpixel accuracy.
        /// </summary>
        FindBestSubPixel
    }

    /// <summary>
    /// Result of a Minos pattern search.
    /// </summary>
    public sealed class MinosSearchResultItem
    {
        /// <summary>
        /// Zero-based result index.
        /// </summary>
        public int Index { get; init; }

        /// <summary>
        /// Name of the matched classifier model.
        /// </summary>
        public string ClassName { get; init; } = string.Empty;

        /// <summary>
        /// Normalized match quality in the range 0.0-1.0.
        /// </summary>
        public double Quality { get; init; }

        /// <summary>
        /// X coordinate of the matched model's reference point.
        /// </summary>
        public double X { get; init; }

        /// <summary>
        /// Y coordinate of the matched model's reference point.
        /// </summary>
        public double Y { get; init; }

        /// <summary>
        /// Horizontal component of the model's OCR advance vector.
        /// </summary>
        public double AdvanceX { get; init; }

        /// <summary>
        /// Vertical component of the model's OCR advance vector.
        /// </summary>
        public double AdvanceY { get; init; }
    }

    /// <summary>
    /// Locates learned patterns in an image using a Minos <see cref="Classifier"/>.
    /// </summary>
    public sealed class MinosSearchNode : Node, IInitializable
    {
        private Classifier? _classifier;
        private double _density = 1.0;
        private double _minQuality = 0.5;
        private int _locality = 10;
        private int _maxResults = 100;

        /// <summary>
        /// Input port that receives the source image.
        /// </summary>
        public Port ImageInput { get; }

        /// <summary>
        /// Output port that passes the source image through.
        /// </summary>
        public Port ImageOutput { get; }

        /// <summary>
        /// Output port that provides the search results.
        /// </summary>
        public Port ResultsOutput { get; }

        /// <summary>
        /// Path to the Minos classifier file (.clf).
        /// </summary>
        public string ClassifierPath { get; set; } = string.Empty;

        /// <summary>
        /// Search operation to perform.
        /// </summary>
        public MinosSearchOperation SearchOperation { get; set; } = MinosSearchOperation.FindAll;

        /// <summary>
        /// Fraction of candidate pixels to scan (0.0-1.0).
        /// </summary>
        public double Density
        {
            get => _density;
            set => _density = double.IsFinite(value) ? Math.Clamp(value, 0.0, 1.0) : 1.0;
        }

        /// <summary>
        /// Minimum normalized quality accepted by the classifier (0.0-1.0).
        /// </summary>
        public double MinQuality
        {
            get => _minQuality;
            set => _minQuality = double.IsFinite(value) ? Math.Clamp(value, 0.0, 1.0) : 0.5;
        }

        /// <summary>
        /// Radius in which no better result may exist when finding all patterns.
        /// </summary>
        public int Locality
        {
            get => _locality;
            set => _locality = Math.Max(0, value);
        }

        /// <summary>
        /// Maximum number of results exposed by the node. A value of 0 disables the limit.
        /// </summary>
        public int MaxResults
        {
            get => _maxResults;
            set => _maxResults = Math.Max(0, value);
        }

        /// <summary>
        /// Number of matches produced by the last execution.
        /// </summary>
        public int ResultCount { get; private set; }

        /// <summary>
        /// Detailed matches from the last execution.
        /// </summary>
        public IReadOnlyList<MinosSearchResultItem> Results { get; private set; } = [];

        /// <inheritdoc/>
        public bool IsInitialized { get; private set; }

        /// <summary>
        /// Creates a Minos search node with image input and image/result outputs.
        /// </summary>
        public MinosSearchNode()
        {
            ImageInput = AddInput("Image", typeof(Image), "The source image in which to locate learned patterns.");
            ImageOutput = AddOutput("Image", typeof(Image), "The source image passed through unchanged.");
            ResultsOutput = AddOutput("Results", typeof(IReadOnlyList<MinosSearchResultItem>), "Patterns located by the Minos classifier.");
        }

        /// <inheritdoc/>
        public void Initialize()
        {
            DisposeClassifier();
            IsInitialized = false;

            var classifier = new Classifier(ClassifierPath);
            try
            {
                classifier.QualityMeasure = QualityFeedback.Normalized;
                classifier.Threshold = MinQuality;
                _classifier = classifier;
                IsInitialized = true;
            }
            catch
            {
                classifier.Dispose();
                throw;
            }
        }

        /// <inheritdoc/>
        public override void Execute()
        {
            if (!IsInitialized || _classifier is null)
                throw new InvalidOperationException($"{nameof(MinosSearchNode)} must be initialized before execution.");

            var source = (Image)ImageInput.Value!;
            if (source.Planes.Count == 0)
                throw new InvalidOperationException("Minos search requires an image with at least one plane.");

            _classifier.Threshold = MinQuality;
            var plane = source.Planes[0];
            var nativeResults = SearchOperation switch
            {
                MinosSearchOperation.FindAll => _classifier.SearchAll(plane, Density, Locality),
                MinosSearchOperation.FindFirst => ToResultArray(_classifier.Search(plane, SearchMode.FindFirst, Density)),
                MinosSearchOperation.FindBest => ToResultArray(_classifier.Search(plane, SearchMode.FindBest, Density)),
                MinosSearchOperation.FindBestSubPixel => ToResultArray(_classifier.Search(plane, SearchMode.FindBestSubPixel, Density)),
                _ => throw new ArgumentOutOfRangeException(nameof(SearchOperation), SearchOperation, "Unsupported Minos search operation.")
            };

            var acceptedResults = nativeResults
                .Where(result => result != SearchResult.Empty && result.Quality >= MinQuality);

            if (MaxResults > 0)
                acceptedResults = acceptedResults.Take(MaxResults);

            var results = acceptedResults
                .Select((result, index) => new MinosSearchResultItem
                {
                    Index = index,
                    ClassName = result.Name,
                    Quality = result.Quality,
                    X = result.X,
                    Y = result.Y,
                    AdvanceX = result.AdvanceVector.X,
                    AdvanceY = result.AdvanceVector.Y
                })
                .ToList();

            Results = results;
            ResultCount = results.Count;
            ImageOutput.Value = source;
            ResultsOutput.Value = results;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            DisposeClassifier();
            IsInitialized = false;
        }

        private static SearchResult[] ToResultArray(SearchResult result)
            => result == SearchResult.Empty ? [] : [result];

        private void DisposeClassifier()
        {
            _classifier?.Dispose();
            _classifier = null;
        }

        // Code generation

        /// <inheritdoc/>
        public override string CodeVariableName => "minosResults";

        /// <inheritdoc/>
        public override IReadOnlyList<string> RequiredUsings => ["System.Collections.Generic", "Stemmer.Cvb.Minos"];

        /// <inheritdoc/>
        public override void EmitCode(CodeEmitContext context)
        {
            var inputVar = context.ResolveInput(ImageInput);
            if (inputVar == null) return;

            var classifierVar = context.GetUniqueVariable("minosClassifier");
            var nativeResultsVar = context.GetUniqueVariable("minosMatches");
            var resultsVar = context.GetUniqueVariable(CodeVariableName);

            context.Builder.AppendLine($"// Minos {SearchOperation} search (density: {CodeEmitContext.FormatDouble(Density)}, min quality: {CodeEmitContext.FormatDouble(MinQuality)})");
            context.Builder.AppendLine($"using var {classifierVar} = new Classifier(@\"{CodeEmitContext.EscapeVerbatim(ClassifierPath)}\");");
            context.Builder.AppendLine($"{classifierVar}.QualityMeasure = QualityFeedback.Normalized;");
            context.Builder.AppendLine($"{classifierVar}.Threshold = {CodeEmitContext.FormatDouble(MinQuality)};");

            if (SearchOperation == MinosSearchOperation.FindAll)
            {
                context.Builder.AppendLine($"var {nativeResultsVar} = {classifierVar}.SearchAll({inputVar}.Planes[0], {CodeEmitContext.FormatDouble(Density)}, {Locality});");
            }
            else
            {
                var nativeMode = SearchOperation switch
                {
                    MinosSearchOperation.FindFirst => "FindFirst",
                    MinosSearchOperation.FindBest => "FindBest",
                    MinosSearchOperation.FindBestSubPixel => "FindBestSubPixel",
                    _ => throw new ArgumentOutOfRangeException(nameof(SearchOperation), SearchOperation, "Unsupported Minos search operation.")
                };
                var singleResultVar = context.GetUniqueVariable("minosMatch");
                context.Builder.AppendLine($"var {singleResultVar} = {classifierVar}.Search({inputVar}.Planes[0], SearchMode.{nativeMode}, {CodeEmitContext.FormatDouble(Density)});");
                context.Builder.AppendLine($"var {nativeResultsVar} = {singleResultVar} == SearchResult.Empty ? new SearchResult[0] : new[] {{ {singleResultVar} }};");
            }

            context.Builder.AppendLine($"var {resultsVar} = new List<SearchResult>();");
            context.Builder.AppendLine($"foreach (var match in {nativeResultsVar})");
            context.Builder.AppendLine("{");
            context.Builder.AppendLine($"    if (match == SearchResult.Empty || match.Quality < {CodeEmitContext.FormatDouble(MinQuality)}) continue;");
            context.Builder.AppendLine($"    {resultsVar}.Add(match);");
            if (MaxResults > 0)
                context.Builder.AppendLine($"    if ({resultsVar}.Count >= {MaxResults}) break;");
            context.Builder.AppendLine("}");

            context.RegisterOutput(ImageOutput, inputVar);
            context.RegisterOutput(ResultsOutput, resultsVar);
        }
    }
}
