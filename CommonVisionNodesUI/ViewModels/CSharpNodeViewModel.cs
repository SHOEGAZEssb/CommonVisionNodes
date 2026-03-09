using CommonVisionNodes;
using CvbImage = Stemmer.Cvb.Image;

namespace CommonVisionNodesUI.ViewModels;

/// <summary>
/// View model for <see cref="CSharpNode"/>. Manages C# code editing and compilation status.
/// </summary>
public partial class CSharpNodeViewModel : NodeViewModel
{
    private readonly CSharpNode _csharpNode;

    [ObservableProperty]
    private string _code = string.Empty;

    [ObservableProperty]
    private CvbImage? _previewImage;

    [ObservableProperty]
    private string? _compilationError;

    [ObservableProperty]
    private bool _hasCompilationError;

    /// <inheritdoc/>
    public override string? Summary => HasCompilationError ? "⚠ Error" : "✓ Ready";

    /// <inheritdoc/>
    public override bool IsEditableWhileRunning => false;

    /// <summary>
    /// Creates a new C# node view model.
    /// </summary>
    /// <param name="node">The underlying C# node.</param>
    /// <param name="x">Initial X position.</param>
    /// <param name="y">Initial Y position.</param>
    public CSharpNodeViewModel(CSharpNode node, double x, double y) : base(node, x, y)
    {
        _csharpNode = node;
        _code = node.Code;
    }

    partial void OnCodeChanged(string value)
    {
        if (!IsSelected) { _code = _csharpNode.Code; return; }
        _csharpNode.Code = value;
    }

    /// <summary>
    /// Updates the preview image from the output and checks for compilation errors.
    /// </summary>
    public override void RefreshPreview()
    {
        PreviewImage = _csharpNode.ImageOutput.Value as CvbImage;
        CompilationError = _csharpNode.LastCompilationError;
        HasCompilationError = !string.IsNullOrEmpty(CompilationError);
        OnPropertyChanged(nameof(Summary));
    }
}
