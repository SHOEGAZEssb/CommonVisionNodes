using CommonVisionNodes.Contracts;

namespace CommonVisionNodesUI.ViewModels;

public partial class ManualTriggerNodeViewModel : NodeViewModel
{
    public ManualTriggerNodeViewModel(NodeDto node, NodeDefinitionDto definition)
        : base(node, definition)
    {
    }

    public event EventHandler? TriggerRequested;

    [ObservableProperty]
    public partial string Status { get; set; } = "Ready";

    public override string? Summary => Status;

    public override bool IsEditableWhileRunning => true;

    public void MarkTriggerQueued()
    {
        Status = "Trigger queued";
    }

    public void MarkTriggerUnavailable()
    {
        Status = "Run the graph first";
    }

    public void MarkTriggerFailed()
    {
        Status = "Trigger failed";
    }

    [RelayCommand]
    private void Trigger()
    {
        TriggerRequested?.Invoke(this, EventArgs.Empty);
    }

    partial void OnStatusChanged(string value)
    {
        RaiseSummaryChanged();
    }
}
