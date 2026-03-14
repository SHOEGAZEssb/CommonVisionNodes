using CommonVisionNodes.Contracts;

namespace CommonVisionNodesUI.ViewModels;

public partial class BinarizeNodeViewModel : NodeViewModel
{
    public BinarizeNodeViewModel(NodeDto node, NodeDefinitionDto definition)
        : base(node, definition)
    {
        _threshold = GetInt("Threshold", 128);
    }

    [ObservableProperty]
    private int _threshold;

    [ObservableProperty]
    private ImagePreviewDto? _previewImage;

    public override string? Summary => $"Threshold {Threshold}";

    partial void OnThresholdChanged(int value)
    {
        SetInt("Threshold", value);
        RaiseSummaryChanged();
    }

    public override void ApplyImagePreview(ImagePreviewDto? preview)
    {
        PreviewImage = preview;
    }
}
