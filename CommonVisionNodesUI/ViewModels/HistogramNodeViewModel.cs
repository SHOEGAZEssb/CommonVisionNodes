using CommonVisionNodes;

namespace CommonVisionNodesUI.ViewModels;

/// <summary>
/// View model for <see cref="HistogramNode"/>. Exposes histogram bin data and statistics.
/// </summary>
public partial class HistogramNodeViewModel : NodeViewModel
{
    private readonly HistogramNode _histogramNode;

    [ObservableProperty]
    private long[] _bins = [];

    [ObservableProperty]
    private double _mean;

    [ObservableProperty]
    private double _stdDev;

    /// <inheritdoc/>
    public override string? Summary => Bins.Length > 0
        ? $"\u03BC {Mean:F1}  \u03C3 {StdDev:F1}"
        : "No data";

    /// <summary>
    /// Creates a new histogram node view model.
    /// </summary>
    /// <param name="node">The underlying histogram node.</param>
    /// <param name="x">Initial X position.</param>
    /// <param name="y">Initial Y position.</param>
    public HistogramNodeViewModel(HistogramNode node, double x, double y) : base(node, x, y)
    {
        _histogramNode = node;
    }

    /// <summary>
    /// Updates the histogram data from the underlying node.
    /// </summary>
    public void RefreshPreview()
    {
        Bins = _histogramNode.Bins;
        Mean = _histogramNode.Mean;
        StdDev = _histogramNode.StdDev;
        OnPropertyChanged(nameof(Summary));
    }
}
