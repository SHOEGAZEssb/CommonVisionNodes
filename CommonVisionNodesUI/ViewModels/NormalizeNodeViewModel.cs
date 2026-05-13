using CommonVisionNodes.Contracts;

namespace CommonVisionNodesUI.ViewModels;

public partial class NormalizeNodeViewModel : NodeViewModel
{
    public NormalizeNodeViewModel(NodeDto node, NodeDefinitionDto definition)
        : base(node, definition)
    {
		OutputMin = GetInt("OutputMin", 0);
		OutputMax = GetInt("OutputMax", 255);
    }

	[ObservableProperty]
	public partial int OutputMin { get; set; }

	[ObservableProperty]
	public partial int OutputMax { get; set; }

	[ObservableProperty]
	public partial ImagePreviewDto? PreviewImage { get; set; }

	public override string? Summary => $"{OutputMin}-{OutputMax}";

    public override bool IsEditableWhileRunning => true;

    partial void OnOutputMinChanged(int value)
    {
        SetInt("OutputMin", value);
        RaiseSummaryChanged();
    }

    partial void OnOutputMaxChanged(int value)
    {
        SetInt("OutputMax", value);
        RaiseSummaryChanged();
    }

    public override void ApplyImagePreview(ImagePreviewDto? preview)
    {
        PreviewImage = preview;
    }
}
