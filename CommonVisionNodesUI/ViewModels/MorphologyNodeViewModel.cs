using CommonVisionNodes.Contracts;

namespace CommonVisionNodesUI.ViewModels;

public partial class MorphologyNodeViewModel : NodeViewModel
{
    private readonly IReadOnlyList<string> _availableOperations;
    private readonly IReadOnlyList<string> _availableKernelSizes;

    public MorphologyNodeViewModel(NodeDto node, NodeDefinitionDto definition)
        : base(node, definition)
    {
        _availableOperations = GetOptions("Operation").Select(option => option.Value).ToList();
        _availableKernelSizes = GetOptions("KernelSize").Select(option => option.Value).ToList();
        _operation = GetString("Operation", _availableOperations.FirstOrDefault() ?? string.Empty);
        _kernelSize = GetString("KernelSize", _availableKernelSizes.FirstOrDefault() ?? string.Empty);
    }

    public IReadOnlyList<string> AvailableOperations => _availableOperations;

    public IReadOnlyList<string> AvailableKernelSizes => _availableKernelSizes;

    private string _operation = string.Empty;

    private string _kernelSize = string.Empty;

    [ObservableProperty]
    private ImagePreviewDto? _previewImage;

    public override string? Summary => string.IsNullOrEmpty(Operation)
        ? "No operation"
        : $"{Operation} / {KernelSize}";

    public override bool IsEditableWhileRunning => true;

    public string Operation
    {
        get => _operation;
        set
        {
            var nextValue = value ?? string.Empty;
            if (string.IsNullOrWhiteSpace(nextValue) && !string.IsNullOrWhiteSpace(_operation))
                return;

            if (SetProperty(ref _operation, nextValue))
            {
                SetString("Operation", nextValue);
                RaiseSummaryChanged();
            }
        }
    }

    public string KernelSize
    {
        get => _kernelSize;
        set
        {
            var nextValue = value ?? string.Empty;
            if (string.IsNullOrWhiteSpace(nextValue) && !string.IsNullOrWhiteSpace(_kernelSize))
                return;

            if (SetProperty(ref _kernelSize, nextValue))
            {
                SetString("KernelSize", nextValue);
                RaiseSummaryChanged();
            }
        }
    }

    public override void ApplyImagePreview(ImagePreviewDto? preview)
    {
        PreviewImage = preview;
    }
}
