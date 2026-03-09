using CommonVisionNodes;
using CvbImage = Stemmer.Cvb.Image;

namespace CommonVisionNodesUI.ViewModels;

/// <summary>
/// View model for <see cref="NormalizeNode"/>. Manages output range and image preview.
/// </summary>
public partial class NormalizeNodeViewModel : NodeViewModel
{
    private readonly NormalizeNode _normalizeNode;

    [ObservableProperty]
    private int _outputMin = 0;

    [ObservableProperty]
    private int _outputMax = 255;

    [ObservableProperty]
    private CvbImage? _previewImage;

    public override string? Summary => $"Range: {OutputMin}–{OutputMax}";

    public override bool IsEditableWhileRunning => true;

    /// <summary>
    /// Creates a new normalize node view model.
    /// </summary>
    /// <param name="node">The underlying normalize node.</param>
    /// <param name="x">Initial X position.</param>
    /// <param name="y">Initial Y position.</param>
    public NormalizeNodeViewModel(NormalizeNode node, double x, double y) : base(node, x, y)
    {
        _normalizeNode = node;
        _outputMin = node.OutputMin;
        _outputMax = node.OutputMax;
    }

    partial void OnOutputMinChanged(int value)
    {
        _normalizeNode.OutputMin = value;
        OnPropertyChanged(nameof(Summary));
    }

    partial void OnOutputMaxChanged(int value)
    {
        _normalizeNode.OutputMax = value;
        OnPropertyChanged(nameof(Summary));
    }

    /// <summary>
    /// Updates the preview image from the normalized output.
    /// </summary>
    public override void RefreshPreview()
    {
        PreviewImage = _normalizeNode.ImageOutput.Value as CvbImage;
    }
}
