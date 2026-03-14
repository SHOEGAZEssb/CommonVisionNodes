using CommonVisionNodes.Contracts;

namespace CommonVisionNodesUI.ViewModels;

public partial class HistogramNodeViewModel : NodeViewModel
{
    public HistogramNodeViewModel(NodeDto node, NodeDefinitionDto definition)
        : base(node, definition)
    {
    }

    [ObservableProperty]
    private long[] _bins = [];

    [ObservableProperty]
    private double _mean;

    [ObservableProperty]
    private double _stdDev;

    public override string? Summary => Bins.Length > 0
        ? $"u {Mean:F1}  s {StdDev:F1}"
        : "No data";

    public override void ApplyHistogramPreview(HistogramPreviewDto preview)
    {
        Bins = preview.Bins.ToArray();
        Mean = preview.Mean;
        StdDev = preview.StdDev;
        RaiseSummaryChanged();
    }
}
