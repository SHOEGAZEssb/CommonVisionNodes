using CommonVisionNodes;
using CvbImage = Stemmer.Cvb.Image;

namespace CommonVisionNodesUI.ViewModels;

public partial class BinarizeNodeViewModel : NodeViewModel
{
    private readonly BinarizeNode _binarizeNode;

    [ObservableProperty]
    private int _threshold = 128;

    [ObservableProperty]
    private CvbImage? _previewImage;

    public override string? Summary => $"Threshold: {Threshold}";

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

    public void RefreshPreview()
    {
        PreviewImage = _binarizeNode.ImageOutput.Value as CvbImage;
    }
}
