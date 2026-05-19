using CommonVisionNodes.Contracts;

namespace CommonVisionNodesUI.ViewModels;

/// <summary>
/// View model for the generic visualizer node.
/// </summary>
public partial class GenericVisualizerNodeViewModel : NodeViewModel
{
    /// <summary>
    /// Creates a generic visualizer node view model.
    /// </summary>
    /// <param name="node">Serialized node instance.</param>
    /// <param name="definition">Catalog definition.</param>
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

    /// <inheritdoc/>
    public override string? Summary => TypeDescription;

    /// <inheritdoc/>
    public override void ApplyImagePreview(ImagePreviewDto? preview)
    {
        PreviewImage = preview;
        TypeDescription = preview is null ? "No data" : $"Image ({preview.Width}x{preview.Height})";
        DisplayText = string.Empty;
        RaiseSummaryChanged();
    }

    /// <inheritdoc/>
    public override void ApplyTextPreview(TextPreviewDto preview)
    {
        PreviewImage = null;
        TypeDescription = preview.TypeDescription;
        DisplayText = preview.DisplayText;
        RaiseSummaryChanged();
    }
}
