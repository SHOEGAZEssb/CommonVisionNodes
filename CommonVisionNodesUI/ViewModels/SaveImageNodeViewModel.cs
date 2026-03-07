using CommonVisionNodes;
using System.IO;
using CvbImage = Stemmer.Cvb.Image;

namespace CommonVisionNodesUI.ViewModels;

public partial class SaveImageNodeViewModel : NodeViewModel
{
    private readonly SaveImageNode _saveImageNode;

    [ObservableProperty]
    private string _filePath = string.Empty;

    [ObservableProperty]
    private CvbImage? _previewImage;

    public override string? Summary => string.IsNullOrEmpty(FilePath)
        ? "No file selected"
        : Path.GetFileName(FilePath);

    public SaveImageNodeViewModel(SaveImageNode node, double x, double y) : base(node, x, y)
    {
        _saveImageNode = node;
        _filePath = node.FilePath;
    }

    partial void OnFilePathChanged(string value)
    {
        _saveImageNode.FilePath = value;
        OnPropertyChanged(nameof(Summary));
    }

    public void RefreshPreview()
    {
        PreviewImage = _saveImageNode.ImageInput.Value as CvbImage;
    }
}
