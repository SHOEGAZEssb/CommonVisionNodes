using CommonVisionNodes;
using CvbImage = Stemmer.Cvb.Image;

namespace CommonVisionNodesUI.ViewModels;

/// <summary>
/// View model for <see cref="SubImageNode"/>. Manages crop area and image preview.
/// </summary>
public partial class SubImageNodeViewModel : NodeViewModel
{
    private readonly SubImageNode _subImageNode;

    [ObservableProperty]
    private int _areaX;

    [ObservableProperty]
    private int _areaY;

    [ObservableProperty]
    private int _areaWidth = 64;

    [ObservableProperty]
    private int _areaHeight = 64;

    [ObservableProperty]
    private CvbImage? _previewImage;

    public override string? Summary => $"{AreaWidth}\u00D7{AreaHeight} @ ({AreaX},{AreaY})";

    /// <summary>
    /// Creates a new sub-image node view model.
    /// </summary>
    /// <param name="node">The underlying sub-image node.</param>
    /// <param name="x">Initial X position.</param>
    /// <param name="y">Initial Y position.</param>
    public SubImageNodeViewModel(SubImageNode node, double x, double y) : base(node, x, y)
    {
        _subImageNode = node;
        _areaX = node.AreaX;
        _areaY = node.AreaY;
        _areaWidth = node.AreaWidth;
        _areaHeight = node.AreaHeight;
    }

    partial void OnAreaXChanged(int value)
    {
        _subImageNode.AreaX = value;
        OnPropertyChanged(nameof(Summary));
    }

    partial void OnAreaYChanged(int value)
    {
        _subImageNode.AreaY = value;
        OnPropertyChanged(nameof(Summary));
    }

    partial void OnAreaWidthChanged(int value)
    {
        _subImageNode.AreaWidth = value;
        OnPropertyChanged(nameof(Summary));
    }

    partial void OnAreaHeightChanged(int value)
    {
        _subImageNode.AreaHeight = value;
        OnPropertyChanged(nameof(Summary));
    }

    /// <summary>
    /// Updates the preview image from the sub-image node's input.
    /// </summary>
    public override void RefreshPreview()
    {
        PreviewImage = _subImageNode.ImageInput.Value as CvbImage;
    }
}
