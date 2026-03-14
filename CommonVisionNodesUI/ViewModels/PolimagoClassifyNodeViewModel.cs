using System.IO;
using CommonVisionNodes.Contracts;

namespace CommonVisionNodesUI.ViewModels;

public partial class PolimagoClassifyNodeViewModel : NodeViewModel
{
    public PolimagoClassifyNodeViewModel(NodeDto node, NodeDefinitionDto definition)
        : base(node, definition)
    {
        _classifierPath = GetString("ClassifierPath");
        _minQuality = GetDouble("MinQuality", 0.5);
    }

    [ObservableProperty]
    private string _classifierPath = string.Empty;

    [ObservableProperty]
    private double _minQuality;

    [ObservableProperty]
    private IReadOnlyList<ClassificationResultDto> _results = [];

    [ObservableProperty]
    private ImagePreviewDto? _previewImage;

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
        Results = preview.Results.ToList();
        OnPropertyChanged(nameof(ResultCount));
        RaiseSummaryChanged();
    }
}
