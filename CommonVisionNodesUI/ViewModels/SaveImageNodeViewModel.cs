using System.IO;
using CommonVisionNodes.Contracts;

namespace CommonVisionNodesUI.ViewModels;

public partial class SaveImageNodeViewModel : NodeViewModel
{
    public SaveImageNodeViewModel(NodeDto node, NodeDefinitionDto definition)
        : base(node, definition)
    {
		FilePath = GetString("FilePath");
    }

	[ObservableProperty]
	public partial string FilePath { get; set; } = string.Empty;

	[ObservableProperty]
	public partial ImagePreviewDto? PreviewImage { get; set; }

	public override string? Summary => string.IsNullOrEmpty(FilePath)
        ? "No output path"
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
