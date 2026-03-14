using System.Collections.ObjectModel;
using CommonVisionNodes.Contracts;

namespace CommonVisionNodesUI.ViewModels;

public record DiscoveredDevice(string DisplayName, string AccessToken);

public partial class DeviceNodeViewModel : NodeViewModel
{
    private readonly Func<Task>? _refreshDevicesAsync;

    public DeviceNodeViewModel(NodeDto node, NodeDefinitionDto definition, Func<Task>? refreshDevicesAsync = null)
        : base(node, definition)
    {
        _refreshDevicesAsync = refreshDevicesAsync;
        _accessToken = GetString("AccessToken");
        RefreshDiscoveredDevices();
    }

    [ObservableProperty]
    private string _accessToken = string.Empty;

    public ObservableCollection<DiscoveredDevice> DiscoveredDevices { get; } = [];

    [ObservableProperty]
    private DiscoveredDevice? _selectedDevice;

    public override string? Summary => string.IsNullOrEmpty(AccessToken)
        ? "No device configured"
        : SelectedDevice?.DisplayName ?? AccessToken;

    partial void OnAccessTokenChanged(string value)
    {
        SetString("AccessToken", value);
        RaiseSummaryChanged();
    }

    partial void OnSelectedDeviceChanged(DiscoveredDevice? value)
    {
        if (value is not null)
            AccessToken = value.AccessToken;
    }

    protected override void OnDefinitionUpdated() => RefreshDiscoveredDevices();

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

        SelectedDevice = DiscoveredDevices.FirstOrDefault(device => device.AccessToken == AccessToken);
    }
}
