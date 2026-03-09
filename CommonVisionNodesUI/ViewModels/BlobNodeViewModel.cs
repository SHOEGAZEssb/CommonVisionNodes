using CommonVisionNodes;
using CvbImage = Stemmer.Cvb.Image;

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
    private int _maxArea;

    [ObservableProperty]
    private int _maxBlobCount;

    [ObservableProperty]
    private bool _invertForeground;

    [ObservableProperty]
    private bool _use8Connectivity;

    [ObservableProperty]
    private int _blobCount;

    [ObservableProperty]
    private IReadOnlyList<BlobInfo> _blobs = [];

    [ObservableProperty]
    private CvbImage? _previewImage;

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
        _maxArea = node.MaxArea;
        _maxBlobCount = node.MaxBlobCount;
        _invertForeground = node.InvertForeground;
        _use8Connectivity = node.Use8Connectivity;
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

    partial void OnMaxAreaChanged(int value)
    {
        _blobNode.MaxArea = value;
        OnPropertyChanged(nameof(Summary));
    }

    partial void OnMaxBlobCountChanged(int value)
    {
        _blobNode.MaxBlobCount = value;
        OnPropertyChanged(nameof(Summary));
    }

    partial void OnInvertForegroundChanged(bool value)
    {
        _blobNode.InvertForeground = value;
        OnPropertyChanged(nameof(Summary));
    }

    partial void OnUse8ConnectivityChanged(bool value)
    {
        _blobNode.Use8Connectivity = value;
        OnPropertyChanged(nameof(Summary));
    }

    /// <summary>
    /// Updates blob results and preview image from the underlying node.
    /// </summary>
    public override void RefreshPreview()
    {
        BlobCount = _blobNode.BlobCount;
        PreviewImage = _blobNode.ImageOutput.Value as CvbImage;
        Blobs = _blobNode.Blobs;
        OnPropertyChanged(nameof(Summary));
    }
}
