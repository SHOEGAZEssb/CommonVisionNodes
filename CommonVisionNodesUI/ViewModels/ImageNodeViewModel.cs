using CommonVisionNodes;
using System.IO;
using CvbImage = Stemmer.Cvb.Image;

namespace CommonVisionNodesUI.ViewModels;

public partial class ImageNodeViewModel : NodeViewModel
{
    private readonly ImageNode _imageNode;

    [ObservableProperty]
    private string _filePath = string.Empty;

    [ObservableProperty]
    private CvbImage? _previewImage;

    public override string? Summary => string.IsNullOrEmpty(FilePath)
        ? "No file selected"
        : Path.GetFileName(FilePath);

    public ImageNodeViewModel(ImageNode node, double x, double y) : base(node, x, y)
    {
        _imageNode = node;
        _filePath = node.FilePath;
    }

    partial void OnFilePathChanged(string value)
    {
        _imageNode.FilePath = value;
        OnPropertyChanged(nameof(Summary));

        if (!string.IsNullOrEmpty(value) && File.Exists(value))
        {
            _imageNode.Initialize();
            RefreshPreview();
        }
    }

    public void RefreshPreview()
    {
        PreviewImage = _imageNode.CachedImage;
    }
}
