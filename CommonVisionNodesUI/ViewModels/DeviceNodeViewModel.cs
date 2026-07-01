using CommonVisionNodes.Contracts;

namespace CommonVisionNodesUI.ViewModels;

/// <summary>
/// UI option for a discovered CVB device.
/// </summary>
/// <param name="DisplayName">User-facing device name.</param>
/// <param name="AccessToken">CVB access token used to open the device.</param>
public record DiscoveredDevice(string DisplayName, string AccessToken);

/// <summary>
/// View model for a camera/device acquisition node.
/// </summary>
public partial class DeviceNodeViewModel : NodeViewModel
{
    private readonly Func<Task>? _refreshDevicesAsync;
    private bool _isSyncingSelectedDevice;
    private DiscoveredDevice? _selectedDevice;

    /// <summary>
    /// Creates a device node view model.
    /// </summary>
    /// <param name="node">Serialized node instance.</param>
    /// <param name="definition">Catalog definition.</param>
    /// <param name="refreshDevicesAsync">Optional callback used to refresh discovered devices.</param>
    public DeviceNodeViewModel(NodeDto node, NodeDefinitionDto definition, Func<Task>? refreshDevicesAsync = null)
        : base(node, definition)
    {
        _refreshDevicesAsync = refreshDevicesAsync;
		AccessToken = GetString("AccessToken");
        RefreshDiscoveredDevices();
    }

	[ObservableProperty]
	public partial string AccessToken { get; set; } = string.Empty;
    /// <summary>
    /// Devices currently exposed by the runtime catalog.
    /// </summary>
    public ObservableCollection<DiscoveredDevice> DiscoveredDevices { get; } = [];

    /// <summary>
    /// Currently selected discovered device.
    /// </summary>
    public DiscoveredDevice? SelectedDevice
    {
        get => _selectedDevice;
        set
        {
            if (value is null && _selectedDevice is not null && !_isSyncingSelectedDevice)
                return;

            if (SetProperty(ref _selectedDevice, value))
            {
                if (value is not null)
                    AccessToken = value.AccessToken;

                RaiseSummaryChanged();
            }
        }
    }

	[ObservableProperty]
	public partial ImagePreviewDto? PreviewImage { get; set; }

    /// <inheritdoc/>
    public override string? Summary => string.IsNullOrEmpty(AccessToken)
        ? "No device configured"
        : SelectedDevice?.DisplayName ?? AccessToken;

    partial void OnAccessTokenChanged(string value)
    {
        SetString("AccessToken", value);
        SyncSelectedDevice();
        RaiseSummaryChanged();
    }

    /// <inheritdoc/>
    protected override void OnDefinitionUpdated() => RefreshDiscoveredDevices();

    /// <inheritdoc/>
    public override void ApplyImagePreview(ImagePreviewDto? preview)
    {
        PreviewImage = preview;
    }

    [RelayCommand]
    private async Task DiscoverDevicesAsync()
    {
        if (_refreshDevicesAsync is not null)
            await _refreshDevicesAsync();
    }

    private void RefreshDiscoveredDevices()
    {
        DiscoveredDevices.Clear();
        foreach (var option in GetOptions("AccessToken"))
            DiscoveredDevices.Add(new DiscoveredDevice(option.Label, option.Value));

        SyncSelectedDevice();
    }

    private void SyncSelectedDevice()
    {
        var selected = DiscoveredDevices.FirstOrDefault(device => string.Equals(device.AccessToken, AccessToken, StringComparison.Ordinal));
        if (ReferenceEquals(SelectedDevice, selected))
            return;

        _isSyncingSelectedDevice = true;
        try
        {
            SelectedDevice = selected;
        }
        finally
        {
            _isSyncingSelectedDevice = false;
        }
    }
}
