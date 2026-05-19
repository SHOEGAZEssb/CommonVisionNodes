using CommonVisionNodes.Contracts;

namespace CommonVisionNodesUI.ViewModels;

/// <summary>
/// View model for a histogram analysis node.
/// </summary>
public partial class HistogramNodeViewModel : NodeViewModel
{
    /// <summary>
    /// Creates a histogram node view model.
    /// </summary>
    /// <param name="node">Serialized node instance.</param>
    /// <param name="definition">Catalog definition.</param>
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

    /// <inheritdoc/>
    public override string? Summary => Bins.Length > 0
        ? $"u {Mean:F1}  s {StdDev:F1}"
        : "No data";

    /// <inheritdoc/>
    public override void ApplyHistogramPreview(HistogramPreviewDto preview)
    {
        Bins = [.. preview.Bins];
        Mean = preview.Mean;
        StdDev = preview.StdDev;
        RaiseSummaryChanged();
    }
}
