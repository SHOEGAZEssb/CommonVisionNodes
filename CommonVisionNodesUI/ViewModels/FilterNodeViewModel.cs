using CommonVisionNodes;
using CvbImage = Stemmer.Cvb.Image;

namespace CommonVisionNodesUI.ViewModels;

/// <summary>
/// View model for <see cref="FilterNode"/>. Manages filter type, kernel size, and preview.
/// </summary>
public partial class FilterNodeViewModel : NodeViewModel
{
    private readonly FilterNode _filterNode;

    [ObservableProperty]
    private FilterType _filterType = FilterType.Gauss;

    [ObservableProperty]
    private KernelSize _kernelSize = KernelSize.Kernel3x3;

    [ObservableProperty]
    private CvbImage? _previewImage;

    /// <inheritdoc/>
    public override string? Summary => $"{FilterType} {KernelSize.ToString().Replace("Kernel", "")}";

    /// <inheritdoc/>
    public override bool IsEditableWhileRunning => true;

    /// <summary>
    /// Available filter types for the combo box.
    /// </summary>
    public FilterType[] AvailableFilterTypes { get; } = Enum.GetValues<FilterType>();

    /// <summary>
    /// Available kernel sizes for the combo box.
    /// </summary>
    public KernelSize[] AvailableKernelSizes { get; } = Enum.GetValues<KernelSize>();

    /// <summary>
    /// Creates a new filter node view model.
    /// </summary>
    /// <param name="node">The underlying filter node.</param>
    /// <param name="x">Initial X position.</param>
    /// <param name="y">Initial Y position.</param>
    public FilterNodeViewModel(FilterNode node, double x, double y) : base(node, x, y)
    {
        _filterNode = node;
        _filterType = node.FilterType;
        _kernelSize = node.KernelSize;
    }

    partial void OnFilterTypeChanged(FilterType value)
    {
        if (!IsSelected) { _filterType = _filterNode.FilterType; return; }
        _filterNode.FilterType = value;
        OnPropertyChanged(nameof(Summary));
    }

    partial void OnKernelSizeChanged(KernelSize value)
    {
        if (!IsSelected) { _kernelSize = _filterNode.KernelSize; return; }
        _filterNode.KernelSize = value;
        OnPropertyChanged(nameof(Summary));
    }

    /// <summary>
    /// Updates the preview image from the filtered output.
    /// </summary>
    public void RefreshPreview()
    {
        PreviewImage = _filterNode.ImageOutput.Value as CvbImage;
    }
}
