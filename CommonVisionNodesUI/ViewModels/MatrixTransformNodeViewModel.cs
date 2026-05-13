using CommonVisionNodes.Contracts;

namespace CommonVisionNodesUI.ViewModels;

public partial class MatrixTransformNodeViewModel : NodeViewModel
{
    public MatrixTransformNodeViewModel(NodeDto node, NodeDefinitionDto definition)
        : base(node, definition)
    {
		Angle = GetDouble("Angle", 0);
		ScaleX = GetDouble("ScaleX", 1.0);
		ScaleY = GetDouble("ScaleY", 1.0);
		TranslateX = GetDouble("TranslateX", 0);
		TranslateY = GetDouble("TranslateY", 0);
    }

	[ObservableProperty]
	public partial double Angle { get; set; }

	[ObservableProperty]
	public partial double ScaleX { get; set; }

	[ObservableProperty]
	public partial double ScaleY { get; set; }

	[ObservableProperty]
	public partial double TranslateX { get; set; }

	[ObservableProperty]
	public partial double TranslateY { get; set; }

	[ObservableProperty]
	public partial ImagePreviewDto? PreviewImage { get; set; }

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
