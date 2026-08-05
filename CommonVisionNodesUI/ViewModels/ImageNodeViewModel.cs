using System.Globalization;
using System.IO;
using CommonVisionNodes.Contracts;

namespace CommonVisionNodesUI.ViewModels;

/// <summary>
/// View model for an image-file or image-folder source node.
/// </summary>
public partial class ImageNodeViewModel : NodeViewModel
{
	private const string FilePathPropertyName = "FilePath";
	private const string SelectedImageIndexPropertyName = "SelectedImageIndex";
	private const string IsPlayingPropertyName = "IsPlaying";
	private const string PlayGlyph = "\uE768";
	private const string StopGlyph = "\uE71A";

	private static readonly string[] SupportedImageExtensions =
	[
		".bmp",
		".dib",
		".jpg",
		".jpeg",
		".png",
		".tif",
		".tiff",
		".gif"
	];

	private string _filePath = string.Empty;
	private int _selectedImageIndex;
	private bool _isPlaying = true;
	private bool _isFolderSource;
	private int _folderImageCount;
	private bool _suppressConfigurationChanged;

	/// <summary>
	/// Creates an image node view model.
	/// </summary>
	/// <param name="node">Serialized node instance.</param>
	/// <param name="definition">Catalog definition.</param>
	public ImageNodeViewModel(NodeDto node, NodeDefinitionDto definition)
		: base(node, definition)
	{
		_filePath = GetString(FilePathPropertyName);
		_selectedImageIndex = GetInt(SelectedImageIndexPropertyName);
		_isPlaying = GetBool(IsPlayingPropertyName, true);
		RefreshFolderState();
		SetSelectedImageIndex(_selectedImageIndex, notifyConfigurationChanged: false);
	}

	/// <summary>
	/// Path to a single source image or a folder containing source images.
	/// </summary>
	public string FilePath
	{
		get => _filePath;
		set
		{
			var nextValue = value ?? string.Empty;
			if (!SetProperty(ref _filePath, nextValue))
				return;

			SetString(FilePathPropertyName, nextValue);
			RefreshFolderState();
			SetSelectedImageIndex(SelectedImageIndex, notifyConfigurationChanged: true);
			RaiseSummaryChanged();
		}
	}

	/// <summary>
	/// Selected folder image index.
	/// </summary>
	public int SelectedImageIndex
	{
		get => _selectedImageIndex;
		set => SetSelectedImageIndex(value, notifyConfigurationChanged: !_suppressConfigurationChanged);
	}

	/// <summary>
	/// Indicates whether folder playback should advance on each graph execution tick.
	/// </summary>
	public bool IsPlaying
	{
		get => _isPlaying;
		set
		{
			if (!SetProperty(ref _isPlaying, value))
				return;

			SetBool(IsPlayingPropertyName, value, notifyConfigurationChanged: !_suppressConfigurationChanged);
			OnPropertyChanged(nameof(PlaybackGlyph));
			OnPropertyChanged(nameof(PlaybackText));
			OnPropertyChanged(nameof(PlaybackToolTip));
			RaiseSummaryChanged();
		}
	}

	/// <summary>
	/// Indicates whether the current source path resolves to a folder.
	/// </summary>
	public bool IsFolderSource
	{
		get => _isFolderSource;
		private set
		{
			if (SetProperty(ref _isFolderSource, value))
				NotifyFolderControlStateChanged();
		}
	}

	/// <summary>
	/// Number of supported images discovered in the selected folder.
	/// </summary>
	public int FolderImageCount
	{
		get => _folderImageCount;
		private set
		{
			if (SetProperty(ref _folderImageCount, Math.Max(0, value)))
				NotifyFolderControlStateChanged();
		}
	}

	/// <summary>
	/// Whether folder transport controls can affect an image.
	/// </summary>
	public bool HasFolderImages => IsFolderSource && FolderImageCount > 0;

	/// <summary>
	/// Whether the source path text box should be editable.
	/// </summary>
	public bool CanEditSourcePath => !IsGraphRunning;

	/// <summary>
	/// Current folder position text.
	/// </summary>
	public string ImagePositionText
	{
		get
		{
			if (!IsFolderSource)
				return string.Empty;

			if (FolderImageCount == 0)
				return "0 / 0";

			return $"{SelectedImageIndex + 1} / {FolderImageCount}";
		}
	}

	/// <summary>
	/// Playback button icon glyph.
	/// </summary>
	public string PlaybackGlyph => IsPlaying ? StopGlyph : PlayGlyph;

	/// <summary>
	/// Playback button label.
	/// </summary>
	public string PlaybackText => IsPlaying ? "Stop" : "Play";

	/// <summary>
	/// Playback button tooltip.
	/// </summary>
	public string PlaybackToolTip => IsPlaying ? "Stop folder playback" : "Play folder images";

	/// <inheritdoc/>
	public override string? Summary
	{
		get
		{
			if (string.IsNullOrWhiteSpace(FilePath))
				return "No source selected";

			if (!IsFolderSource)
				return Path.GetFileName(FilePath);

			var folderName = GetFolderDisplayName(FilePath);
			return FolderImageCount == 0
				? $"{folderName}: no images"
				: $"{folderName}: {ImagePositionText} {PlaybackText}";
		}
	}

	/// <inheritdoc/>
	public override bool IsEditableWhileRunning => true;

	/// <inheritdoc/>
	public override void ApplyImagePreview(ImagePreviewDto? preview)
	{
		PreviewImage = preview;
	}

	/// <inheritdoc/>
	protected override void OnExecutionUpdate(NodeExecutionUpdateDto update)
	{
		if (!TryParseRuntimeState(update.Message, out var selectedIndex, out var imageCount, out var isPlaying))
			return;

		_suppressConfigurationChanged = true;
		try
		{
			IsFolderSource = true;
			FolderImageCount = imageCount;
			SelectedImageIndex = selectedIndex;
			IsPlaying = isPlaying;
		}
		finally
		{
			_suppressConfigurationChanged = false;
		}
	}

	/// <inheritdoc/>
	protected override void OnRuntimeEditStateChanged()
	{
		OnPropertyChanged(nameof(CanEditSourcePath));
	}

	[RelayCommand(CanExecute = nameof(CanCycleFolderImages))]
	private void PreviousImage()
	{
		SelectedImageIndex--;
	}

	[RelayCommand(CanExecute = nameof(CanCycleFolderImages))]
	private void NextImage()
	{
		SelectedImageIndex++;
	}

	[RelayCommand(CanExecute = nameof(CanUseFolderPlayback))]
	private void TogglePlayback()
	{
		IsPlaying = !IsPlaying;
	}

	private bool CanCycleFolderImages() => HasFolderImages;

	private bool CanUseFolderPlayback() => IsFolderSource;

	private void SetSelectedImageIndex(int value, bool notifyConfigurationChanged)
	{
		var nextValue = NormalizeSelectedImageIndex(value);
		if (!SetProperty(ref _selectedImageIndex, nextValue, nameof(SelectedImageIndex)))
			return;

		SetString(
			SelectedImageIndexPropertyName,
			nextValue.ToString(CultureInfo.InvariantCulture),
			notifyConfigurationChanged);

		OnPropertyChanged(nameof(ImagePositionText));
		RaiseSummaryChanged();
	}

	private int NormalizeSelectedImageIndex(int value)
	{
		if (FolderImageCount <= 0)
			return Math.Max(0, value);

		var normalized = value % FolderImageCount;
		return normalized < 0
			? normalized + FolderImageCount
			: normalized;
	}

	private void RefreshFolderState()
	{
		var isFolder = false;
		var imageCount = 0;

		if (!string.IsNullOrWhiteSpace(FilePath))
		{
			try
			{
				isFolder = Directory.Exists(FilePath);
				if (isFolder)
				{
					imageCount = Directory.EnumerateFiles(FilePath)
						.Count(path => SupportedImageExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase));
				}
			}
			catch
			{
				isFolder = false;
				imageCount = 0;
			}
		}

		IsFolderSource = isFolder;
		FolderImageCount = imageCount;
	}

	private void NotifyFolderControlStateChanged()
	{
		OnPropertyChanged(nameof(HasFolderImages));
		OnPropertyChanged(nameof(ImagePositionText));
		OnPropertyChanged(nameof(PlaybackToolTip));
		RaiseSummaryChanged();
		PreviousImageCommand.NotifyCanExecuteChanged();
		NextImageCommand.NotifyCanExecuteChanged();
		TogglePlaybackCommand.NotifyCanExecuteChanged();
	}

	private static string GetFolderDisplayName(string folderPath)
	{
		try
		{
			var name = new DirectoryInfo(folderPath).Name;
			return string.IsNullOrWhiteSpace(name)
				? folderPath
				: name;
		}
		catch
		{
			return folderPath;
		}
	}

	private static bool TryParseRuntimeState(string? message, out int selectedIndex, out int imageCount, out bool isPlaying)
	{
		selectedIndex = 0;
		imageCount = 0;
		isPlaying = true;

		if (string.IsNullOrWhiteSpace(message) || !message.StartsWith("Image ", StringComparison.Ordinal))
			return false;

		var parts = message.Split(' ', StringSplitOptions.RemoveEmptyEntries);
		if (parts.Length < 3)
			return false;

		var separatorIndex = parts[1].IndexOf('/');
		if (separatorIndex <= 0 || separatorIndex >= parts[1].Length - 1)
			return false;

		if (!int.TryParse(parts[1][..separatorIndex], NumberStyles.Integer, CultureInfo.InvariantCulture, out var oneBasedIndex))
			return false;

		if (!int.TryParse(parts[1][(separatorIndex + 1)..], NumberStyles.Integer, CultureInfo.InvariantCulture, out imageCount))
			return false;

		selectedIndex = Math.Max(0, oneBasedIndex - 1);
		isPlaying = string.Equals(parts[2], "Playing", StringComparison.OrdinalIgnoreCase);
		return true;
	}
}
