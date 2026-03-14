using CommonVisionNodes.Contracts;

namespace CommonVisionNodesUI.ViewModels;

public partial class FilterNodeViewModel : NodeViewModel
{
    public FilterNodeViewModel(NodeDto node, NodeDefinitionDto definition)
        : base(node, definition)
    {
        _filterType = GetString("FilterType", GetOptions("FilterType").FirstOrDefault()?.Value ?? string.Empty);
        _kernelSize = GetString("KernelSize", GetOptions("KernelSize").FirstOrDefault()?.Value ?? string.Empty);
    }

    public IReadOnlyList<string> AvailableFilterTypes => GetOptions("FilterType").Select(option => option.Value).ToList();

    public IReadOnlyList<string> AvailableKernelSizes => GetOptions("KernelSize").Select(option => option.Value).ToList();

    [ObservableProperty]
    private string _filterType = string.Empty;

    [ObservableProperty]
    private string _kernelSize = string.Empty;

    [ObservableProperty]
    private ImagePreviewDto? _previewImage;

    public override string? Summary => string.IsNullOrEmpty(FilterType)
        ? "No filter"
        : $"{FilterType} / {KernelSize}";

    public override bool IsEditableWhileRunning => true;

    partial void OnFilterTypeChanged(string value)
    {
        SetString("FilterType", value);
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
