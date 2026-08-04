using CommonVisionNodes.Contracts;
using Cvb.Uno.Toolkit.Helpers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;

namespace CommonVisionNodesUI.Controls;

/// <summary>
/// Displays an image preview with Polimago classification markers.
/// </summary>
public sealed partial class PolimagoImageDisplay : UserControl
{
    private ImagePreviewDto? _currentImage;
    private IReadOnlyList<ClassificationResultDto> _results = [];
    private ImagePreviewDto? _requestedImage;
    private IReadOnlyList<ClassificationResultDto> _requestedResults = [];

    /// <summary>
    /// Creates the Polimago image display control.
    /// </summary>
    public PolimagoImageDisplay()
    {
        this.InitializeComponent();
        SizeChanged += (_, _) => RedrawOverlays();
    }

    /// <summary>
    /// Updates the image and classification overlays as one preview frame.
    /// </summary>
    /// <param name="preview">Preview payload, or <c>null</c> to clear the display.</param>
    /// <param name="results">Classification results belonging to <paramref name="preview"/>.</param>
    public void SetPreview(ImagePreviewDto? preview, IReadOnlyList<ClassificationResultDto> results)
    {
        _requestedImage = preview;
        _requestedResults = results;

        if (preview is null)
        {
            DisplayImage.Clear();
            _currentImage = null;
            _results = results;
            PlaceholderText.Visibility = Visibility.Visible;
            InfoOverlay.Visibility = Visibility.Collapsed;
            OverlayCanvas.Children.Clear();
            return;
        }

        _ = ApplyPreviewAsync(preview);
    }

    private async Task ApplyPreviewAsync(ImagePreviewDto preview)
    {
        var appliedPreview = await DisplayImage.SetImageAsync(preview);
        if (appliedPreview is null || !ReferenceEquals(appliedPreview, _requestedImage))
            return;

        // SetImageAsync may coalesce multiple encoded frames and return the newest image to the
        // oldest awaiting caller. Commit the result snapshot belonging to the image it applied.
        _currentImage = appliedPreview;
        _results = _requestedResults;
        PlaceholderText.Visibility = Visibility.Collapsed;
        InfoOverlay.Visibility = Visibility.Visible;
        InfoText.Text = PreviewImageSourceLoader.GetPreviewInfoText(appliedPreview);
        RedrawOverlays();
    }

    private void RedrawOverlays()
    {
        OverlayCanvas.Children.Clear();

        if (_currentImage is null || _results.Count == 0 || ActualWidth <= 0 || ActualHeight <= 0)
            return;

        var mapping = GetImageMapping();
        if (mapping.scaleX <= 0)
            return;

        foreach (var result in _results)
        {
            var displayX = mapping.offsetX + result.X / mapping.scaleX;
            var displayY = mapping.offsetY + result.Y / mapping.scaleY;

            const double radius = 6;
            var circle = new Ellipse
            {
                Width = radius * 2,
                Height = radius * 2,
                Stroke = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 200, 0)),
                StrokeThickness = 1.5,
                Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(60, 255, 200, 0))
            };
            Canvas.SetLeft(circle, displayX - radius);
            Canvas.SetTop(circle, displayY - radius);
            OverlayCanvas.Children.Add(circle);

            var label = new TextBlock
            {
                Text = $"{result.ClassName} {result.Quality:P0}",
                Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 230, 100)),
                FontSize = 9,
                Background = new SolidColorBrush(Windows.UI.Color.FromArgb(160, 0, 0, 0))
            };
            Canvas.SetLeft(label, displayX + radius + 2);
            Canvas.SetTop(label, displayY - 7);
            OverlayCanvas.Children.Add(label);
        }
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
