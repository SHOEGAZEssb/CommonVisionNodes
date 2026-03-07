using CommonVisionNodes;

namespace CommonVisionNodesUI.ViewModels;

public partial class DeviceNodeViewModel : NodeViewModel
{
    private readonly DeviceNode _deviceNode;

    [ObservableProperty]
    private string _accessToken = string.Empty;

    public override string? Summary => string.IsNullOrEmpty(AccessToken)
        ? "No device configured"
        : AccessToken;

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
}
