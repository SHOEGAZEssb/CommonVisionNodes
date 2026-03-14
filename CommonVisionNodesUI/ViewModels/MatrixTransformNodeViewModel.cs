using CommonVisionNodes.Contracts;

namespace CommonVisionNodesUI.ViewModels;

public partial class MatrixTransformNodeViewModel : NodeViewModel
{
    public MatrixTransformNodeViewModel(NodeDto node, NodeDefinitionDto definition)
        : base(node, definition)
    {
        _angle = GetDouble("Angle", 0);
        _scaleX = GetDouble("ScaleX", 1.0);
        _scaleY = GetDouble("ScaleY", 1.0);
        _translateX = GetDouble("TranslateX", 0);
        _translateY = GetDouble("TranslateY", 0);
    }

    [ObservableProperty]
    private double _angle;

    [ObservableProperty]
    private double _scaleX;

    [ObservableProperty]
    private double _scaleY;

    [ObservableProperty]
    private double _translateX;

    [ObservableProperty]
    private double _translateY;

    [ObservableProperty]
    private ImagePreviewDto? _previewImage;

    public override string? Summary => $"{Angle:F1}°  {ScaleX:F2}x/{ScaleY:F2}x";

    public override bool IsEditableWhileRunning => true;

    partial void OnAngleChanged(double value)
    {
        SetDouble("Angle", value);
        RaiseSummaryChanged();
    }

    partial void OnScaleXChanged(double value)
    {
        SetDouble("ScaleX", value);
        RaiseSummaryChanged();
    }

    partial void OnScaleYChanged(double value)
    {
        SetDouble("ScaleY", value);
        RaiseSummaryChanged();
    }

    partial void OnTranslateXChanged(double value)
    {
        SetDouble("TranslateX", value);
        RaiseSummaryChanged();
    }

    partial void OnTranslateYChanged(double value)
    {
        SetDouble("TranslateY", value);
        RaiseSummaryChanged();
    }

    public override void ApplyImagePreview(ImagePreviewDto? preview)
    {
        PreviewImage = preview;
    }
}
