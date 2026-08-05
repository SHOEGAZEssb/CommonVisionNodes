using CommonVisionNodes.Contracts;

namespace CommonVisionNodesUI.ViewModels;

/// <summary>
/// View model for a binarization node.
/// </summary>
public partial class BinarizeNodeViewModel : NodeViewModel
{
	/// <summary>
	/// Creates a binarization node view model.
	/// </summary>
	/// <param name="node">Serialized node instance.</param>
	/// <param name="definition">Catalog definition.</param>
	public BinarizeNodeViewModel(NodeDto node, NodeDefinitionDto definition)
		: base(node, definition)
	{
		Threshold = GetInt("Threshold", 128);
	}

	[ObservableProperty]
	public partial int Threshold { get; set; }

	/// <inheritdoc/>
	public override string? Summary => $"Threshold {Threshold}";

	partial void OnThresholdChanged(int value)
	{
		SetInt("Threshold", value);
		RaiseSummaryChanged();
	}

	/// <inheritdoc/>
	public override void ApplyImagePreview(ImagePreviewDto? preview)
	{
		PreviewImage = preview;
	}
}
