using CommonVisionNodes.Contracts;

namespace CommonVisionNodesUI.ViewModels;

/// <summary>
/// View model for a time-based trigger node.
/// </summary>
public partial class TimeTriggerNodeViewModel : NodeViewModel
{
	private const string FramesPerSecondPropertyName = "FramesPerSecond";

	private double _framesPerSecond;

	/// <summary>
	/// Creates a time trigger node view model.
	/// </summary>
	/// <param name="node">Serialized node instance.</param>
	/// <param name="definition">Catalog definition.</param>
	public TimeTriggerNodeViewModel(NodeDto node, NodeDefinitionDto definition)
		: base(node, definition)
	{
		FramesPerSecond = GetDouble(FramesPerSecondPropertyName, 1.0);
	}

	/// <inheritdoc/>
	public override string? Summary => $"{FramesPerSecond:0.###} fps";

	/// <inheritdoc/>
	public override bool IsEditableWhileRunning => true;

	/// <summary>
	/// Trigger rate in frames per second.
	/// </summary>
	public double FramesPerSecond
	{
		get => _framesPerSecond;
		set
		{
			if (!double.IsFinite(value))
				return;

			var nextValue = Math.Max(0.0, value);
			if (SetProperty(ref _framesPerSecond, nextValue))
			{
				SetDouble(FramesPerSecondPropertyName, nextValue);
				RaiseSummaryChanged();
			}
		}
	}
}
