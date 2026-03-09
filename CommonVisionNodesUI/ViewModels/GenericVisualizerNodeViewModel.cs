using CommonVisionNodes;
using Stemmer.Cvb;

namespace CommonVisionNodesUI.ViewModels;

/// <summary>
/// View model for <see cref="GenericVisualizerNode"/>.
/// Exposes the last received value and a human-readable description of its runtime type.
/// </summary>
public partial class GenericVisualizerNodeViewModel : NodeViewModel
{
    private readonly GenericVisualizerNode _node;

    [ObservableProperty]
    private object? _lastValue;

    [ObservableProperty]
    private string _typeDescription = "No data";

    /// <inheritdoc/>
    public override string? Summary => TypeDescription;

    /// <summary>
    /// Creates a new generic visualizer node view model.
    /// </summary>
    /// <param name="node">The underlying visualizer node.</param>
    /// <param name="x">Initial X position.</param>
    /// <param name="y">Initial Y position.</param>
    public GenericVisualizerNodeViewModel(GenericVisualizerNode node, double x, double y)
        : base(node, x, y)
    {
        _node = node;
    }

    /// <inheritdoc/>
    public override void RefreshPreview()
    {
        LastValue = _node.LastValue;
        TypeDescription = _node.LastValue switch
        {
            null => "No data",
            Image img when !img.IsDisposed => $"Image ({img.Width}×{img.Height})",
            IReadOnlyList<BlobInfo> blobs => $"BlobInfo × {blobs.Count}",
            IReadOnlyList<BlobRect> rects => $"BlobRect × {rects.Count}",
            IReadOnlyList<PolimagoClassifyResultItem> results => $"ClassifyResult × {results.Count}",
            _ => _node.LastValue.GetType().Name
        };
        OnPropertyChanged(nameof(Summary));
    }
}
