using CommonVisionNodes;
using CvbImage = Stemmer.Cvb.Image;

namespace CommonVisionNodesUI.ViewModels;

/// <summary>
/// View model for <see cref="BinarizeNode"/>. Manages threshold and image preview.
/// </summary>
public partial class BinarizeNodeViewModel : NodeViewModel
{
    private readonly BinarizeNode _binarizeNode;

    [ObservableProperty]
    private int _threshold = 128;

    [ObservableProperty]
    private CvbImage? _previewImage;

    public override string? Summary => $"Threshold: {Threshold}";

    public override bool IsEditableWhileRunning => true;

    /// <summary>
    /// Creates a new binarize node view model.
    /// </summary>
    /// <param name="node">The underlying binarize node.</param>
    /// <param name="x">Initial X position.</param>
    /// <param name="y">Initial Y position.</param>
    public BinarizeNodeViewModel(BinarizeNode node, double x, double y) : base(node, x, y)
    {
        _binarizeNode = node;
        _threshold = node.Threshold;
    }

    partial void OnThresholdChanged(int value)
    {
        _binarizeNode.Threshold = value;
        OnPropertyChanged(nameof(Summary));
    }

    /// <summary>
    /// Updates the preview image from the binarized output.
    /// </summary>
    public void RefreshPreview()
    {
        PreviewImage = _binarizeNode.ImageOutput.Value as CvbImage;
    }
}
