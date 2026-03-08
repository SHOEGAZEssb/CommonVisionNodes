using CommonVisionNodes;
using System.IO;
using CvbImage = Stemmer.Cvb.Image;

namespace CommonVisionNodesUI.ViewModels;

/// <summary>
/// View model for <see cref="ImageNode"/>. Manages file path and image preview.
/// </summary>
public partial class ImageNodeViewModel : NodeViewModel
{
    private readonly ImageNode _imageNode;

    [ObservableProperty]
    private string _filePath = string.Empty;

    [ObservableProperty]
    private CvbImage? _previewImage;

    /// <inheritdoc/>
    public override string? Summary => string.IsNullOrEmpty(FilePath)
        ? "No file selected"
        : Path.GetFileName(FilePath);

    /// <summary>
    /// Creates a new image node view model.
    /// </summary>
    /// <param name="node">The underlying image node.</param>
    /// <param name="x">Initial X position.</param>
    /// <param name="y">Initial Y position.</param>
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

    /// <summary>
    /// Updates the preview image from the underlying node's cached image.
    /// </summary>
    public override void RefreshPreview()
    {
        PreviewImage = _imageNode.CachedImage;
    }
}
