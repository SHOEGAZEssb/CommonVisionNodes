using CommonVisionNodes.Contracts;

namespace CommonVisionNodesUI.ViewModels;

public partial class CSharpNodeViewModel : NodeViewModel
{
    public CSharpNodeViewModel(NodeDto node, NodeDefinitionDto definition)
        : base(node, definition)
    {
		Code = GetString("Code");
    }

	[ObservableProperty]
	public partial string Code { get; set; } = string.Empty;

	[ObservableProperty]
	public partial ImagePreviewDto? PreviewImage { get; set; }

	[ObservableProperty]
	public partial string CompilationError { get; set; } = string.Empty;

	public bool HasCompilationError => !string.IsNullOrWhiteSpace(CompilationError);

    public override string? Summary => HasCompilationError ? "Script error" : "Custom image code";

    partial void OnCodeChanged(string value)
    {
        SetString("Code", value);
        CompilationError = string.Empty;
        OnPropertyChanged(nameof(HasCompilationError));
        RaiseSummaryChanged();
    }

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

    public override void ApplyImagePreview(ImagePreviewDto? preview)
    {
        PreviewImage = preview;
    }
}
