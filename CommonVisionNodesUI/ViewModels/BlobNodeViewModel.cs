using CommonVisionNodes.Contracts;

namespace CommonVisionNodesUI.ViewModels;

public partial class BlobNodeViewModel : NodeViewModel
{
    public BlobNodeViewModel(NodeDto node, NodeDefinitionDto definition)
        : base(node, definition)
    {
        _foregroundThreshold = GetInt("ForegroundThreshold", 128);
        _minArea = GetInt("MinArea", 1);
        _maxArea = GetInt("MaxArea", 0);
        _maxBlobCount = GetInt("MaxBlobCount", 0);
        _invertForeground = GetBool("InvertForeground", false);
        _use8Connectivity = GetBool("Use8Connectivity", false);
    }

    [ObservableProperty]
    private int _foregroundThreshold;

    [ObservableProperty]
    private int _minArea;

    [ObservableProperty]
    private int _maxArea;

    [ObservableProperty]
    private int _maxBlobCount;

    [ObservableProperty]
    private bool _invertForeground;

    [ObservableProperty]
    private bool _use8Connectivity;

    [ObservableProperty]
    private IReadOnlyList<BlobInfoDto> _blobs = [];

    [ObservableProperty]
    private ImagePreviewDto? _previewImage;

    public int BlobCount => Blobs.Count;

    public override string? Summary => $"{BlobCount} blob(s)";

    public override bool IsEditableWhileRunning => true;

    partial void OnForegroundThresholdChanged(int value)
    {
        SetInt("ForegroundThreshold", value);
        RaiseSummaryChanged();
    }

    partial void OnMinAreaChanged(int value)
    {
        SetInt("MinArea", value);
        RaiseSummaryChanged();
    }

    partial void OnMaxAreaChanged(int value)
    {
        SetInt("MaxArea", value);
        RaiseSummaryChanged();
    }

    partial void OnMaxBlobCountChanged(int value)
    {
        SetInt("MaxBlobCount", value);
        RaiseSummaryChanged();
    }

    partial void OnInvertForegroundChanged(bool value)
    {
        SetBool("InvertForeground", value);
        RaiseSummaryChanged();
    }

    partial void OnUse8ConnectivityChanged(bool value)
    {
        SetBool("Use8Connectivity", value);
        RaiseSummaryChanged();
    }

    public override void ApplyBlobPreview(BlobPreviewDto preview)
    {
        PreviewImage = preview.Image;
        Blobs = preview.Blobs.ToList();
        OnPropertyChanged(nameof(BlobCount));
        RaiseSummaryChanged();
    }
}
