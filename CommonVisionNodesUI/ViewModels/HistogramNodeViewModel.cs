using CommonVisionNodes.Contracts;

namespace CommonVisionNodesUI.ViewModels;

public partial class HistogramNodeViewModel : NodeViewModel
{
    public HistogramNodeViewModel(NodeDto node, NodeDefinitionDto definition)
        : base(node, definition)
    {
    }

	[ObservableProperty]
	public partial long[] Bins { get; set; } = [];

	[ObservableProperty]
	public partial double Mean { get; set; }

	[ObservableProperty]
	public partial double StdDev { get; set; }

	public override string? Summary => Bins.Length > 0
        ? $"u {Mean:F1}  s {StdDev:F1}"
        : "No data";

    public override void ApplyHistogramPreview(HistogramPreviewDto preview)
    {
        Bins = [.. preview.Bins];
        Mean = preview.Mean;
        StdDev = preview.StdDev;
        RaiseSummaryChanged();
    }
}
