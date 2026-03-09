using CommonVisionNodes;
using System.IO;

namespace CommonVisionNodesUI.ViewModels;

/// <summary>
/// View model for <see cref="PolimagoClassifyNode"/>. Manages classifier path,
/// quality threshold, and classification results display.
/// </summary>
public partial class PolimagoClassifyNodeViewModel : NodeViewModel
{
    private readonly PolimagoClassifyNode _classifyNode;

    [ObservableProperty]
    private string _classifierPath = string.Empty;

    [ObservableProperty]
    private double _minQuality = 0.5;

    [ObservableProperty]
    private int _resultCount;

    [ObservableProperty]
    private IReadOnlyList<PolimagoClassifyResultItem> _results = [];

    /// <inheritdoc/>
    public override string? Summary => string.IsNullOrEmpty(ClassifierPath)
        ? "No classifier loaded"
        : $"{Path.GetFileName(ClassifierPath)} ({ResultCount} result(s))";

    /// <inheritdoc/>
    public override bool IsEditableWhileRunning => true;

    /// <summary>
    /// Creates a new Polimago classify node view model.
    /// </summary>
    /// <param name="node">The underlying classify node.</param>
    /// <param name="x">Initial X position.</param>
    /// <param name="y">Initial Y position.</param>
    public PolimagoClassifyNodeViewModel(PolimagoClassifyNode node, double x, double y) : base(node, x, y)
    {
        _classifyNode = node;
        _classifierPath = node.ClassifierPath;
        _minQuality = node.MinQuality;
    }

    partial void OnClassifierPathChanged(string value)
    {
        _classifyNode.ClassifierPath = value;
        OnPropertyChanged(nameof(Summary));

        if (!string.IsNullOrEmpty(value) && File.Exists(value))
        {
            _classifyNode.Initialize();
        }
    }

    partial void OnMinQualityChanged(double value)
    {
        _classifyNode.MinQuality = value;
        OnPropertyChanged(nameof(Summary));
    }

    /// <summary>
    /// Updates classification results from the underlying node.
    /// </summary>
    public override void RefreshPreview()
    {
        ResultCount = _classifyNode.ResultCount;
        Results = _classifyNode.Results;
        OnPropertyChanged(nameof(Summary));
    }
}
