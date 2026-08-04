using CommonVisionNodes.Contracts;
using Cvb.Uno.Toolkit.Helpers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;

namespace CommonVisionNodesUI.Controls;

/// <summary>
/// Displays an image preview with CodeReader corner overlays.
/// </summary>
public sealed partial class CodeReaderImageDisplay : UserControl
{
	private sealed class ResultOverlay
	{
		public Polygon Polygon { get; } = new()
		{
			Stroke = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0, 220, 255)),
			StrokeThickness = 1.8,
			Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(35, 0, 220, 255)),
			Points = []
		};

		public Ellipse CenterMarker { get; } = new()
		{
			Stroke = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 230, 100)),
			StrokeThickness = 1.2,
			Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(80, 255, 230, 100))
		};

		public TextBlock Label { get; } = new()
		{
			Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 210, 245, 255)),
			FontSize = 9,
			Background = new SolidColorBrush(Windows.UI.Color.FromArgb(170, 0, 0, 0)),
			Padding = new Thickness(3, 1, 3, 1),
			TextTrimming = TextTrimming.CharacterEllipsis
		};
	}

	private ImagePreviewDto? _currentImage;
	private IReadOnlyList<CodeReaderResultDto> _results = [];
	private readonly List<ResultOverlay> _overlays = [];
	private bool _timeLimitReached;

	/// <summary>
	/// Creates the CodeReader image display control.
	/// </summary>
	public CodeReaderImageDisplay()
	{
		this.InitializeComponent();
		SizeChanged += (_, _) => RedrawOverlays();
	}

	/// <summary>
	/// Updates the image used behind CodeReader overlays.
	/// </summary>
	/// <param name="preview">Preview payload, or <c>null</c> to clear the display.</param>
	public async void SetImage(ImagePreviewDto? preview)
	{
		_currentImage = preview;

		if (preview is null)
		{
			DisplayImage.Clear();
			PlaceholderText.Visibility = Visibility.Visible;
			InfoOverlay.Visibility = Visibility.Collapsed;
			OverlayCanvas.Children.Clear();
			_overlays.Clear();
			return;
		}

		var appliedPreview = await DisplayImage.SetImageAsync(preview);
		if (appliedPreview is null)
			return;

		PlaceholderText.Visibility = Visibility.Collapsed;
		InfoOverlay.Visibility = Visibility.Visible;
		UpdateInfoText(appliedPreview);
		RedrawOverlays();
	}

	/// <summary>
	/// Updates decoded-code overlays.
	/// </summary>
	/// <param name="results">Decoded code results to draw.</param>
	/// <param name="timeLimitReached">Whether decoding hit the configured time limit.</param>
	public void SetResults(IReadOnlyList<CodeReaderResultDto> results, bool timeLimitReached)
	{
		_results = results;
		_timeLimitReached = timeLimitReached;
		UpdateInfoText(_currentImage);
		RedrawOverlays();
	}

	private void RedrawOverlays()
	{
		if (_currentImage is null || _results.Count == 0 || ActualWidth <= 0 || ActualHeight <= 0)
		{
			HideUnusedOverlays(0);
			return;
		}

		var mapping = GetImageMapping();
		if (mapping.scaleX <= 0)
		{
			HideUnusedOverlays(0);
			return;
		}

		for (var i = 0; i < _results.Count; i++)
			DrawResult(mapping, _results[i], i);

		HideUnusedOverlays(_results.Count);
	}

	private void DrawResult((double offsetX, double offsetY, double scaleX, double scaleY) mapping, CodeReaderResultDto result, int overlayIndex)
	{
		var overlay = GetOrCreateOverlay(overlayIndex);
		var hasCorners = result.Corners.Count >= 4;
		overlay.Polygon.Visibility = hasCorners ? Visibility.Visible : Visibility.Collapsed;
		if (hasCorners)
		{
			overlay.Polygon.Points.Clear();
			foreach (var corner in result.Corners.Take(4))
				overlay.Polygon.Points.Add(MapPoint(mapping, corner.X, corner.Y));
		}

		var center = MapPoint(mapping, result.CenterX, result.CenterY);
		const double radius = 4;
		overlay.CenterMarker.Visibility = Visibility.Visible;
		overlay.CenterMarker.Width = radius * 2;
		overlay.CenterMarker.Height = radius * 2;
		Canvas.SetLeft(overlay.CenterMarker, center.X - radius);
		Canvas.SetTop(overlay.CenterMarker, center.Y - radius);

		overlay.Label.Visibility = Visibility.Visible;
		overlay.Label.Text = $"#{result.Index} {result.Symbology}";
		overlay.Label.MaxWidth = Math.Max(60, ActualWidth - center.X - radius - 6);
		Canvas.SetLeft(overlay.Label, center.X + radius + 2);
		Canvas.SetTop(overlay.Label, center.Y - 8);
	}

	private ResultOverlay GetOrCreateOverlay(int index)
	{
		while (_overlays.Count <= index)
		{
			var overlay = new ResultOverlay();
			_overlays.Add(overlay);
			OverlayCanvas.Children.Add(overlay.Polygon);
			OverlayCanvas.Children.Add(overlay.CenterMarker);
			OverlayCanvas.Children.Add(overlay.Label);
		}

		return _overlays[index];
	}

	private void HideUnusedOverlays(int firstUnusedIndex)
	{
		for (var i = firstUnusedIndex; i < _overlays.Count; i++)
		{
			_overlays[i].Polygon.Visibility = Visibility.Collapsed;
			_overlays[i].CenterMarker.Visibility = Visibility.Collapsed;
			_overlays[i].Label.Visibility = Visibility.Collapsed;
		}
	}

	private static Point MapPoint((double offsetX, double offsetY, double scaleX, double scaleY) mapping, double x, double y)
		=> new(mapping.offsetX + x / mapping.scaleX, mapping.offsetY + y / mapping.scaleY);

	private void UpdateInfoText(ImagePreviewDto? preview)
	{
		if (preview is null)
			return;

		var suffix = _timeLimitReached
			? $" | {_results.Count} code(s), time limit"
			: $" | {_results.Count} code(s)";
		InfoText.Text = PreviewImageSourceLoader.GetPreviewInfoText(preview) + suffix;
	}

	private (double offsetX, double offsetY, double scaleX, double scaleY) GetImageMapping()
	{
		if (_currentImage is null || ActualWidth <= 0 || ActualHeight <= 0)
			return (0, 0, 0, 0);

		var imgW = (double)_currentImage.Width;
		var imgH = _currentImage.Height;
		var containerW = ActualWidth;
		var containerH = ActualHeight;
		var imgAspect = imgW / imgH;
		var containerAspect = containerW / containerH;

		double renderedW;
		double renderedH;
		double offsetX;
		double offsetY;

		if (imgAspect > containerAspect)
		{
			renderedW = containerW;
			renderedH = containerW / imgAspect;
			offsetX = 0;
			offsetY = (containerH - renderedH) / 2;
		}
		else
		{
			renderedH = containerH;
			renderedW = containerH * imgAspect;
			offsetX = (containerW - renderedW) / 2;
			offsetY = 0;
		}

		return (offsetX, offsetY, imgW / renderedW, imgH / renderedH);
	}
}
