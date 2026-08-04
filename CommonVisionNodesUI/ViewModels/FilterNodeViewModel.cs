using CommonVisionNodes.Contracts;

namespace CommonVisionNodesUI.ViewModels;

/// <summary>
/// View model for a CVB filter node.
/// </summary>
public partial class FilterNodeViewModel : NodeViewModel
{
	private readonly IReadOnlyList<string> _availableFilterTypes;
	private readonly IReadOnlyList<string> _availableKernelSizes;

	/// <summary>
	/// Creates a filter node view model.
	/// </summary>
	/// <param name="node">Serialized node instance.</param>
	/// <param name="definition">Catalog definition.</param>
	public FilterNodeViewModel(NodeDto node, NodeDefinitionDto definition)
		: base(node, definition)
	{
		_availableFilterTypes = [.. GetOptions("FilterType").Select(option => option.Value)];
		_availableKernelSizes = [.. GetOptions("KernelSize").Select(option => option.Value)];
		_filterType = GetString("FilterType", FirstOptionOrDefault(_availableFilterTypes));
		_kernelSize = GetString("KernelSize", FirstOptionOrDefault(_availableKernelSizes));
	}

	/// <summary>
	/// Available filter type names.
	/// </summary>
	public IReadOnlyList<string> AvailableFilterTypes => _availableFilterTypes;

	/// <summary>
	/// Available kernel size names.
	/// </summary>
	public IReadOnlyList<string> AvailableKernelSizes => _availableKernelSizes;

	private string _filterType = string.Empty;

	private string _kernelSize = string.Empty;

	[ObservableProperty]
	public partial ImagePreviewDto? PreviewImage { get; set; }

	/// <inheritdoc/>
	public override string? Summary => string.IsNullOrEmpty(FilterType)
		? "No filter"
		: $"{FilterType} / {KernelSize}";

	/// <inheritdoc/>
	public override bool IsEditableWhileRunning => true;

	/// <summary>
	/// Selected filter type name.
	/// </summary>
	public string FilterType
	{
		get => _filterType;
		set => SetOptionValue(ref _filterType, value, nameof(FilterType));
	}

	/// <summary>
	/// Selected kernel size name.
	/// </summary>
	public string KernelSize
	{
		get => _kernelSize;
		set => SetOptionValue(ref _kernelSize, value, nameof(KernelSize));
	}

	/// <inheritdoc/>
	public override void ApplyImagePreview(ImagePreviewDto? preview)
	{
		PreviewImage = preview;
	}
}
