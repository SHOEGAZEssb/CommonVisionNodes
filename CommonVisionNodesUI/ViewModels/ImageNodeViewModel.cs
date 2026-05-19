using System.IO;
using CommonVisionNodes.Contracts;

namespace CommonVisionNodesUI.ViewModels;

/// <summary>
/// View model for an image-file source node.
/// </summary>
public partial class ImageNodeViewModel : NodeViewModel
{
    /// <summary>
    /// Creates an image node view model.
    /// </summary>
    /// <param name="node">Serialized node instance.</param>
    /// <param name="definition">Catalog definition.</param>
    public ImageNodeViewModel(NodeDto node, NodeDefinitionDto definition)
        : base(node, definition)
    {
		FilePath = GetString("FilePath");
    }

	[ObservableProperty]
	public partial string FilePath { get; set; } = string.Empty;

	[ObservableProperty]
	public partial ImagePreviewDto? PreviewImage { get; set; }

    /// <inheritdoc/>
    public override string? Summary => string.IsNullOrEmpty(FilePath)
        ? "No file selected"
        : Path.GetFileName(FilePath);

    partial void OnFilePathChanged(string value)
    {
        SetString("FilePath", value);
        RaiseSummaryChanged();
    }

    /// <inheritdoc/>
    public override void ApplyImagePreview(ImagePreviewDto? preview)
    {
        PreviewImage = preview;
    }
}
