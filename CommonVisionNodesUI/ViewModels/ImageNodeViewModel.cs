using System.IO;
using CommonVisionNodes.Contracts;

namespace CommonVisionNodesUI.ViewModels;

public partial class ImageNodeViewModel : NodeViewModel
{
    public ImageNodeViewModel(NodeDto node, NodeDefinitionDto definition)
        : base(node, definition)
    {
		FilePath = GetString("FilePath");
    }

	[ObservableProperty]
	public partial string FilePath { get; set; } = string.Empty;

	[ObservableProperty]
	public partial ImagePreviewDto? PreviewImage { get; set; }

	public override string? Summary => string.IsNullOrEmpty(FilePath)
        ? "No file selected"
        : Path.GetFileName(FilePath);

    partial void OnFilePathChanged(string value)
    {
        SetString("FilePath", value);
        RaiseSummaryChanged();
    }

    public override void ApplyImagePreview(ImagePreviewDto? preview)
    {
        PreviewImage = preview;
    }
}
