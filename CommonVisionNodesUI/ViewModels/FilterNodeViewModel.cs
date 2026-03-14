using CommonVisionNodes.Contracts;

namespace CommonVisionNodesUI.ViewModels;

public partial class FilterNodeViewModel : NodeViewModel
{
    private readonly IReadOnlyList<string> _availableFilterTypes;
    private readonly IReadOnlyList<string> _availableKernelSizes;

    public FilterNodeViewModel(NodeDto node, NodeDefinitionDto definition)
        : base(node, definition)
    {
        _availableFilterTypes = GetOptions("FilterType").Select(option => option.Value).ToList();
        _availableKernelSizes = GetOptions("KernelSize").Select(option => option.Value).ToList();
        _filterType = GetString("FilterType", _availableFilterTypes.FirstOrDefault() ?? string.Empty);
        _kernelSize = GetString("KernelSize", _availableKernelSizes.FirstOrDefault() ?? string.Empty);
    }

    public IReadOnlyList<string> AvailableFilterTypes => _availableFilterTypes;

    public IReadOnlyList<string> AvailableKernelSizes => _availableKernelSizes;

    private string _filterType = string.Empty;

    private string _kernelSize = string.Empty;

    [ObservableProperty]
    private ImagePreviewDto? _previewImage;

    public override string? Summary => string.IsNullOrEmpty(FilterType)
        ? "No filter"
        : $"{FilterType} / {KernelSize}";

    public override bool IsEditableWhileRunning => true;

    public string FilterType
    {
        get => _filterType;
        set
        {
            var nextValue = value ?? string.Empty;
            if (string.IsNullOrWhiteSpace(nextValue) && !string.IsNullOrWhiteSpace(_filterType))
                return;

            if (SetProperty(ref _filterType, nextValue))
            {
                SetString("FilterType", nextValue);
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
