using System.Collections.ObjectModel;
using CommonVisionNodes;
using Stemmer.Cvb;
using Stemmer.Cvb.Driver;

namespace CommonVisionNodesUI.ViewModels;

public record DiscoveredDevice(string DisplayName, string AccessToken);

public partial class DeviceNodeViewModel : NodeViewModel
{
    private readonly DeviceNode _deviceNode;

    [ObservableProperty]
    private string _accessToken = string.Empty;

    public ObservableCollection<DiscoveredDevice> DiscoveredDevices { get; } = [];

    [ObservableProperty]
    private DiscoveredDevice? _selectedDevice;

    public override string? Summary => string.IsNullOrEmpty(AccessToken)
        ? "No device configured"
        : SelectedDevice?.DisplayName ?? AccessToken;

    public DeviceNodeViewModel(DeviceNode node, double x, double y) : base(node, x, y)
    {
        _deviceNode = node;
        _accessToken = node.AccessToken;
    }

    partial void OnAccessTokenChanged(string value)
    {
        _deviceNode.AccessToken = value;
        OnPropertyChanged(nameof(Summary));
    }

    partial void OnSelectedDeviceChanged(DiscoveredDevice? value)
    {
        if (value is not null)
        {
            AccessToken = value.AccessToken;
            _deviceNode.Initialize();
        }
    }

    [RelayCommand]
    private void DiscoverDevices()
    {
        DiscoveredDevices.Clear();
        foreach (var info in DeviceFactory.Discover(DiscoverFlags.IgnoreVins | DiscoverFlags.IncludeMockTL))
        {
            var displayName = info.TryGetProperty(DiscoveryProperties.DeviceModel, out var model)
                ? model
                : info.AccessToken;
            DiscoveredDevices.Add(new DiscoveredDevice(displayName, info.AccessToken));
        }

        if (DiscoveredDevices.Count > 0 && SelectedDevice is null)
            SelectedDevice = DiscoveredDevices[0];
    }
}
