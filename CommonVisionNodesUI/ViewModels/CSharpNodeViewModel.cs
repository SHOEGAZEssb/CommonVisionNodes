using CommonVisionNodes.Contracts;

namespace CommonVisionNodesUI.ViewModels;

/// <summary>
/// View model for a custom C# image-processing node.
/// </summary>
public partial class CSharpNodeViewModel : NodeViewModel
{
	/// <summary>
	/// Creates a C# node view model.
	/// </summary>
	/// <param name="node">Serialized node instance.</param>
	/// <param name="definition">Catalog definition.</param>
	public CSharpNodeViewModel(NodeDto node, NodeDefinitionDto definition)
		: base(node, definition)
	{
		Code = GetString("Code");
	}

	[ObservableProperty]
	public partial string Code { get; set; } = string.Empty;

	[ObservableProperty]
	public partial string CompilationError { get; set; } = string.Empty;

	/// <summary>
	/// Indicates whether the latest backend update reported a compilation or runtime error.
	/// </summary>
	public bool HasCompilationError => !string.IsNullOrWhiteSpace(CompilationError);

	/// <inheritdoc/>
	public override string? Summary => HasCompilationError ? "Script error" : "Custom image code";

	partial void OnCodeChanged(string value)
	{
		SetString("Code", value);
		CompilationError = string.Empty;
		OnPropertyChanged(nameof(HasCompilationError));
		RaiseSummaryChanged();
	}

	/// <inheritdoc/>
	protected override void OnExecutionUpdate(NodeExecutionUpdateDto update)
	{
		if (update.Status == NodeExecutionStatusDto.Failed && !string.IsNullOrWhiteSpace(update.Message))
		{
			CompilationError = update.Message;
			OnPropertyChanged(nameof(HasCompilationError));
			RaiseSummaryChanged();
		}
		else if (update.Status == NodeExecutionStatusDto.Succeeded && !string.IsNullOrWhiteSpace(CompilationError))
		{
			CompilationError = string.Empty;
			OnPropertyChanged(nameof(HasCompilationError));
			RaiseSummaryChanged();
		}
	}

	/// <inheritdoc/>
	public override void ApplyImagePreview(ImagePreviewDto? preview)
	{
		PreviewImage = preview;
	}
}
