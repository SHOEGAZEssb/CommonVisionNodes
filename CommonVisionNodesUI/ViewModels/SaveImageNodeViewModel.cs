using System.IO;
using CommonVisionNodes.Contracts;

namespace CommonVisionNodesUI.ViewModels;

public partial class SaveImageNodeViewModel : NodeViewModel
{
    public SaveImageNodeViewModel(NodeDto node, NodeDefinitionDto definition)
        : base(node, definition)
    {
        _filePath = GetString("FilePath");
    }

    [ObservableProperty]
    private string _filePath = string.Empty;

    [ObservableProperty]
    private ImagePreviewDto? _previewImage;

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
