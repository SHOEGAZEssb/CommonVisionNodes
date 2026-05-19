using CommonVisionNodes.Contracts;

namespace CommonVisionNodesUI.ViewModels;

/// <summary>
/// View model for a manual trigger node.
/// </summary>
public partial class ManualTriggerNodeViewModel : NodeViewModel
{
    /// <summary>
    /// Creates a manual trigger node view model.
    /// </summary>
    /// <param name="node">Serialized node instance.</param>
    /// <param name="definition">Catalog definition.</param>
    public ManualTriggerNodeViewModel(NodeDto node, NodeDefinitionDto definition)
        : base(node, definition)
    {
    }

    /// <summary>
    /// Raised when the user requests a trigger pulse.
    /// </summary>
    public event EventHandler? TriggerRequested;

    [ObservableProperty]
    public partial string Status { get; set; } = "Ready";

    /// <inheritdoc/>
    public override string? Summary => Status;

    /// <inheritdoc/>
    public override bool IsEditableWhileRunning => true;

    /// <summary>
    /// Marks the trigger request as queued.
    /// </summary>
    public void MarkTriggerQueued()
    {
        Status = "Trigger queued";
    }

    /// <summary>
    /// Marks the trigger as unavailable because the graph is not running.
    /// </summary>
    public void MarkTriggerUnavailable()
    {
        Status = "Run the graph first";
    }

    /// <summary>
    /// Marks the trigger request as failed.
    /// </summary>
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
