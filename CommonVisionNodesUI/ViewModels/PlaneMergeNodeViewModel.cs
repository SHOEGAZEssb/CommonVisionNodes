using System.Collections.ObjectModel;
using System.Globalization;
using CommonVisionNodes.Contracts;

namespace CommonVisionNodesUI.ViewModels;

/// <summary>
/// View model for a plane-merging node whose input count follows its saved configuration.
/// </summary>
public partial class PlaneMergeNodeViewModel : NodeViewModel
{
	private const int MaximumPlaneCount = 16;

	/// <summary>
	/// Creates a plane-merging node view model.
	/// </summary>
	/// <param name="node">Serialized node instance.</param>
	/// <param name="definition">Catalog definition.</param>
	public PlaneMergeNodeViewModel(NodeDto node, NodeDefinitionDto definition)
		: base(node, definition)
	{
		PlaneCount = Math.Clamp(GetInt("PlaneCount", 3), 1, MaximumPlaneCount);
		UpdateInputs();
		SynchronizePlaneWeights(ParseWeights(GetString("PlaneWeights")));
	}

	/// <summary>
	/// Number of individual image inputs and output planes.
	/// </summary>
	[ObservableProperty]
	public partial int PlaneCount { get; set; }

	/// <summary>
	/// Editable intensity multipliers for each image input.
	/// </summary>
	public ObservableCollection<PlaneWeightViewModel> PlaneWeights { get; } = [];

	/// <inheritdoc/>
	public override string? Summary => $"{PlaneCount} plane(s)";

	/// <inheritdoc/>
	public override bool IsEditableWhileRunning => true;

	/// <summary>
	/// Plane count is locked during execution because it changes the graph topology.
	/// </summary>
	public bool IsPlaneCountEditable => !IsGraphRunning;

	/// <inheritdoc/>
	protected override void OnRuntimeEditStateChanged()
		=> OnPropertyChanged(nameof(IsPlaneCountEditable));

	partial void OnPlaneCountChanged(int value)
	{
		var normalized = Math.Clamp(value, 1, MaximumPlaneCount);
		if (value != normalized)
		{
			PlaneCount = normalized;
			return;
		}

		UpdateInputs();
		SynchronizePlaneWeights();
		SetInt("PlaneCount", value);
		RaiseSummaryChanged();
	}

	private void UpdateInputs()
	{
		SetInputPorts(Enumerable.Range(0, PlaneCount).Select(index => new PortDto
		{
			Name = $"Plane {index}",
			Type = "Image",
			Direction = PortDirectionDto.Input,
			Description = $"A single-plane image used as output plane {index}."
		}));
	}

	private void SynchronizePlaneWeights(IReadOnlyList<double>? desiredWeights = null)
	{
		for (var index = 0; index < PlaneCount; index++)
		{
			var weight = desiredWeights is not null && index < desiredWeights.Count
				? desiredWeights[index]
				: index < PlaneWeights.Count ? PlaneWeights[index].Weight : 1.0;

			if (index < PlaneWeights.Count)
				PlaneWeights[index].Weight = weight;
			else
				PlaneWeights.Add(new PlaneWeightViewModel(index, weight, PersistPlaneWeights));
		}

		while (PlaneWeights.Count > PlaneCount)
			PlaneWeights.RemoveAt(PlaneWeights.Count - 1);

		PersistPlaneWeights();
	}

	private void PersistPlaneWeights()
		=> SetString("PlaneWeights", string.Join(',', PlaneWeights.Select(weight => weight.Weight.ToString("G17", CultureInfo.InvariantCulture))));

	private static IReadOnlyList<double> ParseWeights(string value)
		=> value.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
			.Select(weight => double.TryParse(weight, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) && double.IsFinite(parsed)
				? Math.Max(0, parsed)
				: 1.0)
			.ToList();
}

/// <summary>
/// One editable Plane Merge intensity multiplier.
/// </summary>
public sealed class PlaneWeightViewModel : ObservableObject
{
	private readonly Action _onChanged;
	private double _weight;

	/// <summary>
	/// Creates an editable plane weight.
	/// </summary>
	/// <param name="index">Zero-based plane index.</param>
	/// <param name="weight">Initial multiplier.</param>
	/// <param name="onChanged">Callback used to persist the value.</param>
	public PlaneWeightViewModel(int index, double weight, Action onChanged)
	{
		Index = index;
		_weight = Normalize(weight);
		_onChanged = onChanged;
	}

	/// <summary>
	/// Zero-based input-plane index.
	/// </summary>
	public int Index { get; }

	/// <summary>
	/// User-facing input-plane label.
	/// </summary>
	public string Label => $"Plane {Index}";

	/// <summary>
	/// Non-negative intensity multiplier.
	/// </summary>
	public double Weight
	{
		get => _weight;
		set
		{
			var normalized = Normalize(value);
			if (!SetProperty(ref _weight, normalized))
				return;

			_onChanged();
		}
	}

	private static double Normalize(double value)
		=> double.IsFinite(value) ? Math.Max(0, value) : 1.0;
}
