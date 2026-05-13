using CommonVisionNodes.Contracts;

namespace CommonVisionNodesUI.ViewModels;

public partial class GenericVisualizerNodeViewModel : NodeViewModel
{
    public GenericVisualizerNodeViewModel(NodeDto node, NodeDefinitionDto definition)
        : base(node, definition)
    {
    }

	[ObservableProperty]
	public partial ImagePreviewDto? PreviewImage { get; set; }

	[ObservableProperty]
	public partial string TypeDescription { get; set; } = "No data";

	[ObservableProperty]
	public partial string DisplayText { get; set; } = string.Empty;

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
