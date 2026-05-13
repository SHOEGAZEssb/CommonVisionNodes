using System.IO;
using CommonVisionNodes.Contracts;

namespace CommonVisionNodesUI.ViewModels;

public partial class PolimagoClassifyNodeViewModel : NodeViewModel
{
    public PolimagoClassifyNodeViewModel(NodeDto node, NodeDefinitionDto definition)
        : base(node, definition)
    {
		ClassifierPath = GetString("ClassifierPath");
		MinQuality = GetDouble("MinQuality", 0.5);
    }

	[ObservableProperty]
	public partial string ClassifierPath { get; set; } = string.Empty;

	[ObservableProperty]
	public partial double MinQuality { get; set; }

	[ObservableProperty]
	public partial IReadOnlyList<ClassificationResultDto> Results { get; set; } = [];

	[ObservableProperty]
	public partial ImagePreviewDto? PreviewImage { get; set; }

	public int ResultCount => Results.Count;

    public override string? Summary => string.IsNullOrEmpty(ClassifierPath)
        ? "No classifier loaded"
        : $"{Path.GetFileName(ClassifierPath)} ({ResultCount} result(s))";

    public override bool IsEditableWhileRunning => true;

    partial void OnClassifierPathChanged(string value)
    {
        SetString("ClassifierPath", value);
        RaiseSummaryChanged();
    }

    partial void OnMinQualityChanged(double value)
    {
        SetDouble("MinQuality", value);
        RaiseSummaryChanged();
    }

    public override void ApplyClassificationPreview(ClassificationPreviewDto preview)
    {
        PreviewImage = preview.Image;
        Results = [.. preview.Results];
        OnPropertyChanged(nameof(ResultCount));
        RaiseSummaryChanged();
    }
}
