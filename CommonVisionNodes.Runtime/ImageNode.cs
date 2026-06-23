using Stemmer.Cvb;

namespace CommonVisionNodes.Runtime
{
    /// <summary>
    /// Loads an image from a file or cycles through images from a folder.
    /// </summary>
    public sealed class ImageNode : Node, IInitializable, ITriggerableNode
    {
        private static readonly string[] SupportedImageExtensions =
        [
            ".bmp",
            ".dib",
            ".jpg",
            ".jpeg",
            ".png",
            ".tif",
            ".tiff",
            ".gif"
        ];

        private Image? _cachedImage;
        private string[] _imagePaths = [];
        private string? _cachedImagePath;
        private int _selectedImageIndex;
        private bool _hasExecutedFolderFrame;

        /// <summary>
        /// Optional trigger input that gates when the image is sent downstream.
        /// </summary>
        public Port TriggerInput { get; }

        /// <summary>
        /// The loaded image, available after initialization.
        /// </summary>
        public Image? CachedImage => _cachedImage;

        /// <summary>
        /// Number of image files discovered when <see cref="FilePath"/> points to a folder.
        /// </summary>
        public int ImageCount => _imagePaths.Length;

        /// <summary>
        /// Indicates whether <see cref="FilePath"/> resolved to a folder during initialization.
        /// </summary>
        public bool IsFolderSource { get; private set; }

        /// <summary>
        /// Output port that provides the loaded image.
        /// </summary>
        public Port ImageOutput { get; }

        /// <summary>
        /// Path to the image file or folder to load.
        /// </summary>
        public string FilePath { get; set; } = string.Empty;

        /// <summary>
        /// Index of the folder image that should be sent on the next execution.
        /// </summary>
        public int SelectedImageIndex
        {
            get => _selectedImageIndex;
            set => SetSelectedImageIndex(value, resetCycle: true);
        }

        /// <summary>
        /// When folder mode is active, advances to the next image after each execution.
        /// </summary>
        public bool IsPlaying { get; set; } = true;

        /// <inheritdoc/>
        public bool IsInitialized { get; private set; }

        /// <summary>
        /// Creates an image source node with an optional trigger input and one image output.
        /// </summary>
        public ImageNode()
        {
            TriggerInput = AddInput("Trigger", typeof(TriggerSignal), "Optional trigger that controls when the image is sent.");
            ImageOutput = AddOutput("Image", typeof(Image), "The image loaded from the configured file path.");
        }

        /// <inheritdoc/>
        public void Initialize()
        {
            DisposeCachedImage();
            _imagePaths = [];
            IsFolderSource = false;
            _hasExecutedFolderFrame = false;

            if (Directory.Exists(FilePath))
            {
                _imagePaths = GetImageFiles(FilePath);
                if (_imagePaths.Length == 0)
                    throw new InvalidOperationException($"Folder '{FilePath}' does not contain any supported image files.");

                IsFolderSource = true;
                SetSelectedImageIndex(SelectedImageIndex, resetCycle: false);
                LoadSelectedFolderImage();
            }
            else
            {
                _cachedImage = Image.FromFile(FilePath);
                _cachedImagePath = FilePath;
            }

            IsInitialized = true;
        }

        /// <inheritdoc/>
        public override void Execute()
        {
            if (!IsInitialized)
                throw new InvalidOperationException($"{nameof(ImageNode)} must be initialized before execution.");

            if (IsFolderSource)
            {
                if (IsPlaying && _hasExecutedFolderFrame)
                    SetSelectedImageIndex(SelectedImageIndex + 1, resetCycle: false);

                SetSelectedImageIndex(SelectedImageIndex, resetCycle: false);
                LoadSelectedFolderImage();
                _hasExecutedFolderFrame = true;
            }

            ImageOutput.Value = _cachedImage;
        }

        /// <summary>
        /// Selects the next image when folder mode is active.
        /// </summary>
        public void SelectNextImage()
        {
            if (_imagePaths.Length == 0)
                return;

            SetSelectedImageIndex(SelectedImageIndex + 1, resetCycle: true);
        }

        /// <summary>
        /// Selects the previous image when folder mode is active.
        /// </summary>
        public void SelectPreviousImage()
        {
            if (_imagePaths.Length == 0)
                return;

            SetSelectedImageIndex(SelectedImageIndex - 1, resetCycle: true);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            DisposeCachedImage();
            _imagePaths = [];
            IsFolderSource = false;
            _hasExecutedFolderFrame = false;
            IsInitialized = false;
        }

        private void LoadSelectedFolderImage()
        {
            if (_imagePaths.Length == 0)
                throw new InvalidOperationException($"Folder '{FilePath}' does not contain any supported image files.");

            var path = _imagePaths[NormalizeIndex(SelectedImageIndex)];
            if (string.Equals(_cachedImagePath, path, StringComparison.OrdinalIgnoreCase) && _cachedImage is not null)
                return;

            DisposeCachedImage();
            _cachedImage = Image.FromFile(path);
            _cachedImagePath = path;
        }

        private void SetSelectedImageIndex(int index, bool resetCycle)
        {
            _selectedImageIndex = NormalizeIndex(index);
            if (resetCycle)
                _hasExecutedFolderFrame = false;
        }

        private int NormalizeIndex(int index)
        {
            if (_imagePaths.Length == 0)
                return Math.Max(0, index);

            var normalized = index % _imagePaths.Length;
            return normalized < 0
                ? normalized + _imagePaths.Length
                : normalized;
        }

        private void DisposeCachedImage()
        {
            _cachedImage?.Dispose();
            _cachedImage = null;
            _cachedImagePath = null;
        }

        private static string[] GetImageFiles(string folderPath)
            => [.. Directory.EnumerateFiles(folderPath)
                .Where(path => SupportedImageExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)];

        // Code generation

        /// <inheritdoc/>
        public override string CodeVariableName => "sourceImage";

        /// <inheritdoc/>
        public override IReadOnlyList<string> RequiredUsings
            => Directory.Exists(FilePath)
                ? ["System", "System.IO", "System.Linq"]
                : [];

        /// <inheritdoc/>
        public override void EmitCode(CodeEmitContext context)
        {
            var varName = context.GetUniqueVariable(CodeVariableName);
            if (Directory.Exists(FilePath))
            {
                context.Builder.AppendLine("// Load selected image from folder");
                context.Builder.AppendLine($"var {varName}Paths = Directory.GetFiles(@\"{CodeEmitContext.EscapeVerbatim(FilePath)}\")");
                context.Builder.AppendLine("    .Where(path => new[] { \".bmp\", \".dib\", \".jpg\", \".jpeg\", \".png\", \".tif\", \".tiff\", \".gif\" }");
                context.Builder.AppendLine("        .Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))");
                context.Builder.AppendLine("    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)");
                context.Builder.AppendLine("    .ToArray();");
                context.Builder.AppendLine($"using var {varName} = Image.FromFile({varName}Paths[{SelectedImageIndex} % {varName}Paths.Length]);");
            }
            else
            {
                context.Builder.AppendLine("// Load image from file");
                context.Builder.AppendLine($"using var {varName} = Image.FromFile(@\"{CodeEmitContext.EscapeVerbatim(FilePath)}\");");
            }
            context.RegisterOutput(ImageOutput, varName);
        }
    }
}
