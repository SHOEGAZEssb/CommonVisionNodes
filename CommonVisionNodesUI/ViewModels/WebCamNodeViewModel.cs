using CommonVisionNodes.Contracts;

namespace CommonVisionNodesUI.ViewModels;

/// <summary>
/// UI option for a discovered webcam.
/// </summary>
/// <param name="DisplayName">User-facing webcam name.</param>
/// <param name="DeviceId">Stable DirectShow device path used to open the webcam.</param>
public record DiscoveredWebCam(string DisplayName, string DeviceId);

/// <summary>
/// View model for a local DirectShow webcam input node.
/// </summary>
public partial class WebCamNodeViewModel : NodeViewModel
{
	private readonly Func<Task>? _refreshWebCamsAsync;
	private bool _isSyncingSelectedWebCam;
	private DiscoveredWebCam? _selectedWebCam;

	/// <summary>Creates a webcam node view model.</summary>
	public WebCamNodeViewModel(NodeDto node, NodeDefinitionDto definition, Func<Task>? refreshWebCamsAsync = null)
		: base(node, definition)
	{
		_refreshWebCamsAsync = refreshWebCamsAsync;
		DeviceId = GetString(nameof(DeviceId));
		RefreshDiscoveredWebCams();
	}

	[ObservableProperty]
	public partial string DeviceId { get; set; } = string.Empty;

	/// <summary>Webcams currently exposed by the runtime catalog.</summary>
	public ObservableCollection<DiscoveredWebCam> DiscoveredWebCams { get; } = [];

	/// <summary>Currently selected webcam.</summary>
	public DiscoveredWebCam? SelectedWebCam
	{
		get => _selectedWebCam;
		set
		{
			if (value is null && _selectedWebCam is not null && !_isSyncingSelectedWebCam)
				return;

			if (SetProperty(ref _selectedWebCam, value))
			{
				if (value is not null)
					DeviceId = value.DeviceId;

				RaiseSummaryChanged();
			}
		}
	}

	/// <inheritdoc/>
	public override string? Summary => string.IsNullOrEmpty(DeviceId)
		? "No webcam configured"
		: SelectedWebCam?.DisplayName ?? "Selected webcam unavailable";

	partial void OnDeviceIdChanged(string value)
	{
		SetString(nameof(DeviceId), value);
		SyncSelectedWebCam();
		RaiseSummaryChanged();
	}

	/// <inheritdoc/>
	protected override void OnDefinitionUpdated() => RefreshDiscoveredWebCams();

	/// <inheritdoc/>
	public override void ApplyImagePreview(ImagePreviewDto? preview)
	{
		PreviewImage = preview;
	}

	[RelayCommand]
	private async Task RefreshWebCamsAsync()
	{
		if (_refreshWebCamsAsync is not null)
			await _refreshWebCamsAsync();
	}

	private void RefreshDiscoveredWebCams()
	{
		DiscoveredWebCams.Clear();
		foreach (var option in GetOptions(nameof(DeviceId)))
			DiscoveredWebCams.Add(new DiscoveredWebCam(option.Label, option.Value));

		SyncSelectedWebCam();
	}

	private void SyncSelectedWebCam()
	{
		var selected = DiscoveredWebCams.FirstOrDefault(webCam => string.Equals(webCam.DeviceId, DeviceId, StringComparison.OrdinalIgnoreCase));
		if (ReferenceEquals(SelectedWebCam, selected))
			return;

		_isSyncingSelectedWebCam = true;
		try
		{
			SelectedWebCam = selected;
		}
		finally
		{
			_isSyncingSelectedWebCam = false;
		}
	}
}
