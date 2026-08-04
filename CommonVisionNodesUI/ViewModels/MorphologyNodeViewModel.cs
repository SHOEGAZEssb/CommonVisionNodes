using CommonVisionNodes.Contracts;

namespace CommonVisionNodesUI.ViewModels;

/// <summary>
/// View model for a morphology node.
/// </summary>
public partial class MorphologyNodeViewModel : NodeViewModel
{
	private readonly IReadOnlyList<string> _availableOperations;
	private readonly IReadOnlyList<string> _availableKernelSizes;

	/// <summary>
	/// Creates a morphology node view model.
	/// </summary>
	/// <param name="node">Serialized node instance.</param>
	/// <param name="definition">Catalog definition.</param>
	public MorphologyNodeViewModel(NodeDto node, NodeDefinitionDto definition)
		: base(node, definition)
	{
		_availableOperations = [.. GetOptions("Operation").Select(option => option.Value)];
		_availableKernelSizes = [.. GetOptions("KernelSize").Select(option => option.Value)];
		_operation = GetString("Operation", FirstOptionOrDefault(_availableOperations));
		_kernelSize = GetString("KernelSize", FirstOptionOrDefault(_availableKernelSizes));
	}

	/// <summary>
	/// Available morphology operation names.
	/// </summary>
	public IReadOnlyList<string> AvailableOperations => _availableOperations;

	/// <summary>
	/// Available kernel size names.
	/// </summary>
	public IReadOnlyList<string> AvailableKernelSizes => _availableKernelSizes;

	private string _operation = string.Empty;

	private string _kernelSize = string.Empty;

	[ObservableProperty]
	public partial ImagePreviewDto? PreviewImage { get; set; }

	/// <inheritdoc/>
	public override string? Summary => string.IsNullOrEmpty(Operation)
		? "No operation"
		: $"{Operation} / {KernelSize}";

	/// <inheritdoc/>
	public override bool IsEditableWhileRunning => true;

	/// <summary>
	/// Selected morphology operation name.
	/// </summary>
	public string Operation
	{
		get => _operation;
		set => SetOptionValue(ref _operation, value, nameof(Operation));
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
