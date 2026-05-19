using CommonVisionNodes.Contracts;

namespace CommonVisionNodesUI.ViewModels;

/// <summary>
/// View model for an image normalization node.
/// </summary>
public partial class NormalizeNodeViewModel : NodeViewModel
{
    /// <summary>
    /// Creates a normalization node view model.
    /// </summary>
    /// <param name="node">Serialized node instance.</param>
    /// <param name="definition">Catalog definition.</param>
    public NormalizeNodeViewModel(NodeDto node, NodeDefinitionDto definition)
        : base(node, definition)
    {
		OutputMin = GetInt("OutputMin", 0);
		OutputMax = GetInt("OutputMax", 255);
    }

	[ObservableProperty]
	public partial int OutputMin { get; set; }

	[ObservableProperty]
	public partial int OutputMax { get; set; }

	[ObservableProperty]
	public partial ImagePreviewDto? PreviewImage { get; set; }

    /// <inheritdoc/>
    public override string? Summary => $"{OutputMin}-{OutputMax}";

    /// <inheritdoc/>
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

    /// <inheritdoc/>
    public override void ApplyImagePreview(ImagePreviewDto? preview)
    {
        PreviewImage = preview;
    }
}
