using System.IO;
using CommonVisionNodes.Contracts;

namespace CommonVisionNodesUI.ViewModels;

public partial class ImageNodeViewModel : NodeViewModel
{
    public ImageNodeViewModel(NodeDto node, NodeDefinitionDto definition)
        : base(node, definition)
    {
        _filePath = GetString("FilePath");
    }

    [ObservableProperty]
    private string _filePath = string.Empty;

    [ObservableProperty]
    private ImagePreviewDto? _previewImage;

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
