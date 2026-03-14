using CommonVisionNodes.Contracts;

namespace CommonVisionNodesUI.ViewModels;

public partial class GenericVisualizerNodeViewModel : NodeViewModel
{
    public GenericVisualizerNodeViewModel(NodeDto node, NodeDefinitionDto definition)
        : base(node, definition)
    {
    }

    [ObservableProperty]
    private ImagePreviewDto? _previewImage;

    [ObservableProperty]
    private string _typeDescription = "No data";

    [ObservableProperty]
    private string _displayText = string.Empty;

    public override string? Summary => TypeDescription;

    public override void ApplyImagePreview(ImagePreviewDto? preview)
    {
        PreviewImage = preview;
        TypeDescription = preview is null ? "No data" : $"Image ({preview.Width}x{preview.Height})";
        DisplayText = string.Empty;
        RaiseSummaryChanged();
    }

    public override void ApplyTextPreview(TextPreviewDto preview)
    {
        PreviewImage = null;
        TypeDescription = preview.TypeDescription;
        DisplayText = preview.DisplayText;
        RaiseSummaryChanged();
    }
}
