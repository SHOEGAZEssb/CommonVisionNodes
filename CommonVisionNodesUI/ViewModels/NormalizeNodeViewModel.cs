using CommonVisionNodes.Contracts;

namespace CommonVisionNodesUI.ViewModels;

public partial class NormalizeNodeViewModel : NodeViewModel
{
    public NormalizeNodeViewModel(NodeDto node, NodeDefinitionDto definition)
        : base(node, definition)
    {
        _outputMin = GetInt("OutputMin", 0);
        _outputMax = GetInt("OutputMax", 255);
    }

    [ObservableProperty]
    private int _outputMin;

    [ObservableProperty]
    private int _outputMax;

    [ObservableProperty]
    private ImagePreviewDto? _previewImage;

    public override string? Summary => $"{OutputMin}-{OutputMax}";

    public override bool IsEditableWhileRunning => true;

    partial void OnOutputMinChanged(int value)
    {
        SetInt("OutputMin", value);
        RaiseSummaryChanged();
    }

    partial void OnOutputMaxChanged(int value)
    {
        SetInt("OutputMax", value);
        RaiseSummaryChanged();
    }

    public override void ApplyImagePreview(ImagePreviewDto? preview)
    {
        PreviewImage = preview;
    }
}
