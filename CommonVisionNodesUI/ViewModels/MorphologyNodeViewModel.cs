using CommonVisionNodes;
using CvbImage = Stemmer.Cvb.Image;

namespace CommonVisionNodesUI.ViewModels;

/// <summary>
/// View model for <see cref="MorphologyNode"/>. Manages operation type, kernel size, and preview.
/// </summary>
public partial class MorphologyNodeViewModel : NodeViewModel
{
    private readonly MorphologyNode _morphologyNode;

    [ObservableProperty]
    private MorphologyOperation _operation = MorphologyOperation.Dilate;

    [ObservableProperty]
    private KernelSize _kernelSize = KernelSize.Kernel3x3;

    [ObservableProperty]
    private CvbImage? _previewImage;

    /// <inheritdoc/>
    public override string? Summary => $"{Operation} {KernelSize.ToString().Replace("Kernel", "")}";

    /// <inheritdoc/>
    public override bool IsEditableWhileRunning => true;

    /// <summary>
    /// Available morphological operations for the combo box.
    /// </summary>
    public MorphologyOperation[] AvailableOperations { get; } = Enum.GetValues<MorphologyOperation>();

    /// <summary>
    /// Available kernel sizes for the combo box.
    /// </summary>
    public KernelSize[] AvailableKernelSizes { get; } = Enum.GetValues<KernelSize>();

    /// <summary>
    /// Creates a new morphology node view model.
    /// </summary>
    /// <param name="node">The underlying morphology node.</param>
    /// <param name="x">Initial X position.</param>
    /// <param name="y">Initial Y position.</param>
    public MorphologyNodeViewModel(MorphologyNode node, double x, double y) : base(node, x, y)
    {
        _morphologyNode = node;
        _operation = node.Operation;
        _kernelSize = node.KernelSize;
    }

    partial void OnOperationChanged(MorphologyOperation value)
    {
        if (!IsSelected) { _operation = _morphologyNode.Operation; return; }
        _morphologyNode.Operation = value;
        OnPropertyChanged(nameof(Summary));
    }

    partial void OnKernelSizeChanged(KernelSize value)
    {
        if (!IsSelected) { _kernelSize = _morphologyNode.KernelSize; return; }
        _morphologyNode.KernelSize = value;
        OnPropertyChanged(nameof(Summary));
    }

    /// <summary>
    /// Updates the preview image from the morphology output.
    /// </summary>
    public override void RefreshPreview()
    {
        PreviewImage = _morphologyNode.ImageOutput.Value as CvbImage;
    }
}
