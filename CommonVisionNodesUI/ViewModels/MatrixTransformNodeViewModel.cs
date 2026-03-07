using CommonVisionNodes;
using CvbImage = Stemmer.Cvb.Image;

namespace CommonVisionNodesUI.ViewModels;

public partial class MatrixTransformNodeViewModel : NodeViewModel
{
    private readonly MatrixTransformNode _transformNode;

    [ObservableProperty]
    private double _angle;

    [ObservableProperty]
    private double _scaleX = 1.0;

    [ObservableProperty]
    private double _scaleY = 1.0;

    [ObservableProperty]
    private double _translateX;

    [ObservableProperty]
    private double _translateY;

    [ObservableProperty]
    private CvbImage? _previewImage;

    public override string? Summary => $"R:{Angle:F1}\u00B0  S:{ScaleX:F2}\u00D7{ScaleY:F2}";

    public override bool IsEditableWhileRunning => true;

    public MatrixTransformNodeViewModel(MatrixTransformNode node, double x, double y) : base(node, x, y)
    {
        _transformNode = node;
        _angle = node.Angle;
        _scaleX = node.ScaleX;
        _scaleY = node.ScaleY;
        _translateX = node.TranslateX;
        _translateY = node.TranslateY;
    }

    partial void OnAngleChanged(double value)
    {
        _transformNode.Angle = value;
        OnPropertyChanged(nameof(Summary));
    }

    partial void OnScaleXChanged(double value)
    {
        _transformNode.ScaleX = value;
        OnPropertyChanged(nameof(Summary));
    }

    partial void OnScaleYChanged(double value)
    {
        _transformNode.ScaleY = value;
        OnPropertyChanged(nameof(Summary));
    }

    partial void OnTranslateXChanged(double value)
    {
        _transformNode.TranslateX = value;
    }

    partial void OnTranslateYChanged(double value)
    {
        _transformNode.TranslateY = value;
    }

    public void RefreshPreview()
    {
        PreviewImage = _transformNode.ImageOutput.Value as CvbImage;
    }
}
