using CommonVisionNodes.Contracts;

namespace CommonVisionNodesUI.ViewModels;

/// <summary>
/// View model for a crop/sub-image node.
/// </summary>
public partial class SubImageNodeViewModel : NodeViewModel
{
    /// <summary>
    /// Creates a sub-image node view model.
    /// </summary>
    /// <param name="node">Serialized node instance.</param>
    /// <param name="definition">Catalog definition.</param>
    public SubImageNodeViewModel(NodeDto node, NodeDefinitionDto definition)
        : base(node, definition)
    {
		AreaX = GetInt("AreaX", 0);
		AreaY = GetInt("AreaY", 0);
		AreaWidth = GetInt("AreaWidth", 64);
		AreaHeight = GetInt("AreaHeight", 64);
    }

	[ObservableProperty]
	public partial int AreaX { get; set; }

	[ObservableProperty]
	public partial int AreaY { get; set; }

	[ObservableProperty]
	public partial int AreaWidth { get; set; }

	[ObservableProperty]
	public partial int AreaHeight { get; set; }

	[ObservableProperty]
	public partial ImagePreviewDto? PreviewImage { get; set; }

    /// <inheritdoc/>
    public override string? Summary => $"({AreaX}, {AreaY}) {AreaWidth}x{AreaHeight}";

    partial void OnAreaXChanged(int value)
    {
        SetInt("AreaX", value);
        RaiseSummaryChanged();
    }

    partial void OnAreaYChanged(int value)
    {
        SetInt("AreaY", value);
        RaiseSummaryChanged();
    }

    partial void OnAreaWidthChanged(int value)
    {
        SetInt("AreaWidth", value);
        RaiseSummaryChanged();
    }

    partial void OnAreaHeightChanged(int value)
    {
        SetInt("AreaHeight", value);
        RaiseSummaryChanged();
    }

    /// <inheritdoc/>
    public override void ApplyImagePreview(ImagePreviewDto? preview)
    {
        PreviewImage = preview;
    }
}
