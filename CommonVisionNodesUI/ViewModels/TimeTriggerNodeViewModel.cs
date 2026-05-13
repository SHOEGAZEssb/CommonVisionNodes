using CommonVisionNodes.Contracts;

namespace CommonVisionNodesUI.ViewModels;

public partial class TimeTriggerNodeViewModel : NodeViewModel
{
    private double _intervalSeconds;

    public TimeTriggerNodeViewModel(NodeDto node, NodeDefinitionDto definition)
        : base(node, definition)
    {
        IntervalSeconds = GetDouble("IntervalSeconds", 1.0);
    }

    public override string? Summary => $"{IntervalSeconds:0.###} s";

    public override bool IsEditableWhileRunning => true;

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
