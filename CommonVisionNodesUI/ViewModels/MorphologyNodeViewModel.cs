using CommonVisionNodes.Contracts;

namespace CommonVisionNodesUI.ViewModels;

public partial class MorphologyNodeViewModel : NodeViewModel
{
    public MorphologyNodeViewModel(NodeDto node, NodeDefinitionDto definition)
        : base(node, definition)
    {
        _operation = GetString("Operation", GetOptions("Operation").FirstOrDefault()?.Value ?? string.Empty);
        _kernelSize = GetString("KernelSize", GetOptions("KernelSize").FirstOrDefault()?.Value ?? string.Empty);
    }

    public IReadOnlyList<string> AvailableOperations => GetOptions("Operation").Select(option => option.Value).ToList();

    public IReadOnlyList<string> AvailableKernelSizes => GetOptions("KernelSize").Select(option => option.Value).ToList();

    [ObservableProperty]
    private string _operation = string.Empty;

    [ObservableProperty]
    private string _kernelSize = string.Empty;

    [ObservableProperty]
    private ImagePreviewDto? _previewImage;

    public override string? Summary => string.IsNullOrEmpty(Operation)
        ? "No operation"
        : $"{Operation} / {KernelSize}";

    public override bool IsEditableWhileRunning => true;

    partial void OnOperationChanged(string value)
    {
        SetString("Operation", value);
        RaiseSummaryChanged();
    }

    partial void OnKernelSizeChanged(string value)
    {
        SetString("KernelSize", value);
        RaiseSummaryChanged();
    }

    public override void ApplyImagePreview(ImagePreviewDto? preview)
    {
        PreviewImage = preview;
    }
}
