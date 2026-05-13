using CommonVisionNodes.Contracts;

namespace CommonVisionNodesUI.ViewModels;

public partial class GevServerNodeViewModel : NodeViewModel
{
    private string _localAddress = "127.0.0.1";
    private string _driverType = "Socket";
    private PropertyOptionDto? _selectedAdapter;
    private PropertyOptionDto? _selectedDriverType;

    public GevServerNodeViewModel(NodeDto node, NodeDefinitionDto definition)
        : base(node, definition)
    {
        RefreshOptions();
        _localAddress = GetString("LocalAddress", AvailableAdapters.FirstOrDefault()?.Value ?? "127.0.0.1");
        _driverType = GetString("DriverType", AvailableDriverTypes.FirstOrDefault()?.Value ?? "Socket");
        ResendBuffersCount = GetInt("ResendBuffersCount");
        EnsureSelectionsAreValid();
    }

    public ObservableCollection<PropertyOptionDto> AvailableAdapters { get; } = [];

    public ObservableCollection<PropertyOptionDto> AvailableDriverTypes { get; } = [];

    [ObservableProperty]
    public partial int ResendBuffersCount { get; set; }

    [ObservableProperty]
    public partial string Status { get; set; } = "Stopped.";

    [ObservableProperty]
    public partial ImagePreviewDto? PreviewImage { get; set; }

    public override string? Summary => string.IsNullOrWhiteSpace(Status)
        ? $"{GetAdapterLabel(LocalAddress)} ({DriverType})"
        : Status;

    public string LocalAddress
    {
        get => _localAddress;
        set
        {
            var nextValue = value ?? string.Empty;
            if (string.IsNullOrWhiteSpace(nextValue) && !string.IsNullOrWhiteSpace(_localAddress))
                return;

            if (SetProperty(ref _localAddress, nextValue))
            {
                SetString("LocalAddress", nextValue);
                SyncSelectedAdapter();
                RaiseSummaryChanged();
            }
        }
    }

    public string DriverType
    {
        get => _driverType;
        set
        {
            var nextValue = value ?? string.Empty;
            if (string.IsNullOrWhiteSpace(nextValue) && !string.IsNullOrWhiteSpace(_driverType))
                return;

            if (SetProperty(ref _driverType, nextValue))
            {
                SetString("DriverType", nextValue);
                SyncSelectedDriverType();
                RaiseSummaryChanged();
            }
        }
    }

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

    protected override void OnExecutionUpdate(NodeExecutionUpdateDto update)
    {
        if (!string.IsNullOrWhiteSpace(update.Message))
            Status = update.Message;
    }

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

    public override void ApplyImagePreview(ImagePreviewDto? preview)
    {
        PreviewImage = preview;
    }

    private void RefreshOptions()
    {
        RefreshOptions("LocalAddress", AvailableAdapters);
        RefreshOptions("DriverType", AvailableDriverTypes);
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
