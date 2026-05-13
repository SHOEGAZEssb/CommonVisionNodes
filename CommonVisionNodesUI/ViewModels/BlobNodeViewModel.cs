using CommonVisionNodes.Contracts;

namespace CommonVisionNodesUI.ViewModels;

public partial class BlobNodeViewModel : NodeViewModel
{
    public BlobNodeViewModel(NodeDto node, NodeDefinitionDto definition)
        : base(node, definition)
    {
		ForegroundThreshold = GetInt("ForegroundThreshold", 128);
		MinArea = GetInt("MinArea", 1);
		MaxArea = GetInt("MaxArea", 0);
		MaxBlobCount = GetInt("MaxBlobCount", 0);
		InvertForeground = GetBool("InvertForeground", false);
		Use8Connectivity = GetBool("Use8Connectivity", false);
    }

	[ObservableProperty]
	public partial int ForegroundThreshold { get; set; }

	[ObservableProperty]
	public partial int MinArea { get; set; }

	[ObservableProperty]
	public partial int MaxArea { get; set; }

	[ObservableProperty]
	public partial int MaxBlobCount { get; set; }

	[ObservableProperty]
	public partial bool InvertForeground { get; set; }

	[ObservableProperty]
	public partial bool Use8Connectivity { get; set; }

	[ObservableProperty]
	public partial IReadOnlyList<BlobInfoDto> Blobs { get; set; } = [];

	[ObservableProperty]
	public partial ImagePreviewDto? PreviewImage { get; set; }

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
        Blobs = [.. preview.Blobs];
        OnPropertyChanged(nameof(BlobCount));
        RaiseSummaryChanged();
    }
}
