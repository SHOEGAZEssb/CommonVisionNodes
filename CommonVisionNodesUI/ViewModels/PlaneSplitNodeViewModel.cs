using CommonVisionNodes.Contracts;

namespace CommonVisionNodesUI.ViewModels;

/// <summary>
/// View model for a plane-splitting node whose output count follows its saved configuration.
/// </summary>
public partial class PlaneSplitNodeViewModel : NodeViewModel
{
	private const int MaximumPlaneCount = 16;
	private readonly IReadOnlyList<string> _availableModes;
	private string _mode = string.Empty;

	/// <summary>
	/// Creates a plane-splitting node view model.
	/// </summary>
	/// <param name="node">Serialized node instance.</param>
	/// <param name="definition">Catalog definition.</param>
	public PlaneSplitNodeViewModel(NodeDto node, NodeDefinitionDto definition)
		: base(node, definition)
	{
		_availableModes = [.. GetOptions("Mode").Select(option => option.Value)];
		PlaneCount = Math.Clamp(GetInt("PlaneCount", 3), 1, MaximumPlaneCount);
		_mode = GetString("Mode", FirstOptionOrDefault(_availableModes, "Copy"));
		UpdateOutputs();
	}

	/// <summary>
	/// Number of input planes and individual image outputs.
	/// </summary>
	[ObservableProperty]
	public partial int PlaneCount { get; set; }

	/// <summary>
	/// Available source-pixel handling modes.
	/// </summary>
	public IReadOnlyList<string> AvailableModes => _availableModes;

	/// <summary>
	/// Whether output planes copy or link their source pixels.
	/// </summary>
	public string Mode
	{
		get => _mode;
		set
		{
			if (SetOptionValue(ref _mode, value, nameof(Mode)))
				RaiseSummaryChanged();
		}
	}

	/// <inheritdoc/>
	public override string? Summary => $"{PlaneCount} plane(s) / {Mode}";

	partial void OnPlaneCountChanged(int value)
	{
		var normalized = Math.Clamp(value, 1, MaximumPlaneCount);
		if (value != normalized)
		{
			PlaneCount = normalized;
			return;
		}

		UpdateOutputs();
		SetInt("PlaneCount", value);
		RaiseSummaryChanged();
	}

	private void UpdateOutputs()
	{
		SetOutputPorts(Enumerable.Range(0, PlaneCount).Select(index => new PortDto
		{
			Name = $"Plane {index}",
			Type = "Image",
			Direction = PortDirectionDto.Output,
			Description = $"A single-plane image sourced from input plane {index}."
		}));
	}
}
