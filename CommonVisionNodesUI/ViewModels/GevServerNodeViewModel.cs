using CommonVisionNodes.Contracts;

namespace CommonVisionNodesUI.ViewModels;

/// <summary>
/// View model for a GigE Vision server output node.
/// </summary>
public partial class GevServerNodeViewModel : NodeViewModel
{
	private string _localAddress = "127.0.0.1";
	private string _driverType = "Socket";
	private PropertyOptionDto? _selectedAdapter;
	private PropertyOptionDto? _selectedDriverType;

	/// <summary>
	/// Creates a GigE Vision server node view model.
	/// </summary>
	/// <param name="node">Serialized node instance.</param>
	/// <param name="definition">Catalog definition.</param>
	public GevServerNodeViewModel(NodeDto node, NodeDefinitionDto definition)
		: base(node, definition)
	{
		RefreshOptions();
		_localAddress = GetString("LocalAddress", AvailableAdapters.Count > 0 ? AvailableAdapters[0].Value : "127.0.0.1");
		_driverType = GetString("DriverType", AvailableDriverTypes.Count > 0 ? AvailableDriverTypes[0].Value : "Socket");
		ResendBuffersCount = GetInt("ResendBuffersCount");
		EnsureSelectionsAreValid();
	}

	/// <summary>
	/// Network adapters available for server binding.
	/// </summary>
	public ObservableCollection<PropertyOptionDto> AvailableAdapters { get; } = [];

	/// <summary>
	/// GigE Vision driver options available for the server.
	/// </summary>
	public ObservableCollection<PropertyOptionDto> AvailableDriverTypes { get; } = [];

	[ObservableProperty]
	public partial int ResendBuffersCount { get; set; }

	[ObservableProperty]
	public partial string Status { get; set; } = "Stopped.";

	[ObservableProperty]
	public partial ImagePreviewDto? PreviewImage { get; set; }

	/// <inheritdoc/>
	public override string? Summary => string.IsNullOrWhiteSpace(Status)
		? $"{GetAdapterLabel(LocalAddress)} ({DriverType})"
		: Status;

	/// <summary>
	/// Selected local adapter IPv4 address.
	/// </summary>
	public string LocalAddress
	{
		get => _localAddress;
		set
		{
			if (SetOptionValue(ref _localAddress, value, nameof(LocalAddress)))
				SyncSelectedAdapter();
		}
	}

	/// <summary>
	/// Selected GigE Vision driver type.
	/// </summary>
	public string DriverType
	{
		get => _driverType;
		set
		{
			if (SetOptionValue(ref _driverType, value, nameof(DriverType)))
				SyncSelectedDriverType();
		}
	}

	/// <summary>
	/// Selected adapter option.
	/// </summary>
	public PropertyOptionDto? SelectedAdapter
	{
		get => _selectedAdapter;
		set
		{
			if (value is null && _selectedAdapter is not null)
				return;

			if (SetProperty(ref _selectedAdapter, value) && value is not null)
				LocalAddress = value.Value;
		}
	}

	/// <summary>
	/// Selected driver type option.
	/// </summary>
	public PropertyOptionDto? SelectedDriverType
	{
		get => _selectedDriverType;
		set
		{
			if (value is null && _selectedDriverType is not null)
				return;

			if (SetProperty(ref _selectedDriverType, value) && value is not null)
				DriverType = value.Value;
		}
	}

	partial void OnResendBuffersCountChanged(int value)
	{
		SetInt("ResendBuffersCount", Math.Max(0, value));
	}

	partial void OnStatusChanged(string value)
	{
		RaiseSummaryChanged();
	}

	/// <inheritdoc/>
	protected override void OnExecutionUpdate(NodeExecutionUpdateDto update)
	{
		if (!string.IsNullOrWhiteSpace(update.Message))
			Status = update.Message;
	}

	/// <inheritdoc/>
	protected override void OnExecutionState(ExecutionStateDto state)
	{
		Status = state.Status switch
		{
			ExecutionStatusDto.Stopping => "Stopping.",
			ExecutionStatusDto.Stopped or ExecutionStatusDto.Completed => "Stopped.",
			ExecutionStatusDto.Failed => "Stopped after failure.",
			_ => Status
		};
	}

	/// <inheritdoc/>
	public override void ApplyImagePreview(ImagePreviewDto? preview)
	{
		PreviewImage = preview;
	}

	private void RefreshOptions()
	{
		RefreshOptions(nameof(LocalAddress), AvailableAdapters);
		RefreshOptions(nameof(DriverType), AvailableDriverTypes);
	}

	private void EnsureSelectionsAreValid()
	{
		if (AvailableAdapters.Count > 0 && !AvailableAdapters.Any(option => string.Equals(option.Value, LocalAddress, StringComparison.OrdinalIgnoreCase)))
			LocalAddress = AvailableAdapters[0].Value;

		if (AvailableDriverTypes.Count > 0 && !AvailableDriverTypes.Any(option => string.Equals(option.Value, DriverType, StringComparison.OrdinalIgnoreCase)))
			DriverType = AvailableDriverTypes[0].Value;

		SyncSelectedAdapter();
		SyncSelectedDriverType();
	}

	/// <inheritdoc/>
	protected override void OnDefinitionUpdated()
	{
		RefreshOptions();
		EnsureSelectionsAreValid();
		RaiseSummaryChanged();
	}

	private void RefreshOptions(string propertyName, ObservableCollection<PropertyOptionDto> target)
	{
		target.Clear();
		foreach (var option in GetOptions(propertyName))
			target.Add(option);
	}

	private string GetAdapterLabel(string address)
		=> AvailableAdapters.FirstOrDefault(option => string.Equals(option.Value, address, StringComparison.OrdinalIgnoreCase))?.Label
			?? address;

	private void SyncSelectedAdapter()
	{
		var selected = AvailableAdapters.FirstOrDefault(option => string.Equals(option.Value, LocalAddress, StringComparison.OrdinalIgnoreCase));
		if (!ReferenceEquals(SelectedAdapter, selected))
			SelectedAdapter = selected;
	}

	private void SyncSelectedDriverType()
	{
		var selected = AvailableDriverTypes.FirstOrDefault(option => string.Equals(option.Value, DriverType, StringComparison.OrdinalIgnoreCase));
		if (!ReferenceEquals(SelectedDriverType, selected))
			SelectedDriverType = selected;
	}
}
