using CommonVisionNodes.Contracts;

namespace CommonVisionNodesUI.ViewModels;

/// <summary>
/// View model for a time-based trigger node.
/// </summary>
public partial class TimeTriggerNodeViewModel : NodeViewModel
{
    private double _intervalSeconds;

    /// <summary>
    /// Creates a time trigger node view model.
    /// </summary>
    /// <param name="node">Serialized node instance.</param>
    /// <param name="definition">Catalog definition.</param>
    public TimeTriggerNodeViewModel(NodeDto node, NodeDefinitionDto definition)
        : base(node, definition)
    {
        IntervalSeconds = GetDouble("IntervalSeconds", 1.0);
    }

    /// <inheritdoc/>
    public override string? Summary => $"{IntervalSeconds:0.###} s";

    /// <inheritdoc/>
    public override bool IsEditableWhileRunning => true;

    /// <summary>
    /// Seconds between emitted trigger signals.
    /// </summary>
    public double IntervalSeconds
    {
        get => _intervalSeconds;
        set
        {
            if (!double.IsFinite(value))
                return;

            var nextValue = Math.Max(0.0, value);
            if (SetProperty(ref _intervalSeconds, nextValue))
            {
                SetDouble("IntervalSeconds", nextValue);
                RaiseSummaryChanged();
            }
        }
    }
}
