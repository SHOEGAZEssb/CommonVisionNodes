using CommonVisionNodes;

namespace CommonVisionNodesUI.ViewModels;

/// <summary>
/// View model for <see cref="BlobNode"/>. Exposes blob analysis settings and results.
/// </summary>
public partial class BlobNodeViewModel : NodeViewModel
{
    private readonly BlobNode _blobNode;

    [ObservableProperty]
    private int _foregroundThreshold = 128;

    [ObservableProperty]
    private int _minArea = 1;

    [ObservableProperty]
    private int _blobCount;

    [ObservableProperty]
    private IReadOnlyList<BlobInfo> _blobs = [];

    /// <inheritdoc/>
    public override string? Summary => $"{BlobCount} blob(s)";

    /// <inheritdoc/>
    public override bool IsEditableWhileRunning => true;

    /// <summary>
    /// Creates a new blob node view model.
    /// </summary>
    /// <param name="node">The underlying blob node.</param>
    /// <param name="x">Initial X position.</param>
    /// <param name="y">Initial Y position.</param>
    public BlobNodeViewModel(BlobNode node, double x, double y) : base(node, x, y)
    {
        _blobNode = node;
        _foregroundThreshold = node.ForegroundThreshold;
        _minArea = node.MinArea;
    }

    partial void OnForegroundThresholdChanged(int value)
    {
        _blobNode.ForegroundThreshold = value;
        OnPropertyChanged(nameof(Summary));
    }

    partial void OnMinAreaChanged(int value)
    {
        _blobNode.MinArea = value;
        OnPropertyChanged(nameof(Summary));
    }

    /// <summary>
    /// Updates blob results from the underlying node.
    /// </summary>
    public override void RefreshPreview()
    {
        BlobCount = _blobNode.BlobCount;
        Blobs = _blobNode.Blobs;
        OnPropertyChanged(nameof(Summary));
    }
}
