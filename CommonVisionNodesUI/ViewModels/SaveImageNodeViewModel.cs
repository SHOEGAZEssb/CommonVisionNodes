using CommonVisionNodes;
using System.IO;
using CvbImage = Stemmer.Cvb.Image;

namespace CommonVisionNodesUI.ViewModels;

/// <summary>
/// View model for <see cref="SaveImageNode"/>. Manages file path and image preview.
/// </summary>
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

    /// <summary>
    /// Creates a new save image node view model.
    /// </summary>
    /// <param name="node">The underlying save image node.</param>
    /// <param name="x">Initial X position.</param>
    /// <param name="y">Initial Y position.</param>
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

    /// <summary>
    /// Updates the preview image from the node's input port.
    /// </summary>
    public override void RefreshPreview()
    {
        PreviewImage = _saveImageNode.ImageInput.Value as CvbImage;
    }
}
