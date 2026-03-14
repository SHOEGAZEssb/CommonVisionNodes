using CommonVisionNodes.Contracts;
using CommonVisionNodesUI.Helpers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;

namespace CommonVisionNodesUI.Controls;

public sealed partial class PolimagoImageDisplay : UserControl
{
    private ImagePreviewDto? _currentImage;
    private IReadOnlyList<ClassificationResultDto> _results = [];

    public PolimagoImageDisplay()
    {
        this.InitializeComponent();
        SizeChanged += (_, _) => RedrawOverlays();
    }

    public async void SetImage(ImagePreviewDto? preview)
    {
        _currentImage = preview;

        if (preview is null)
        {
            DisplayImage.Source = null;
            PlaceholderText.Visibility = Visibility.Visible;
            InfoOverlay.Visibility = Visibility.Collapsed;
            OverlayCanvas.Children.Clear();
            return;
        }

        await PreviewImageSourceLoader.SetImageAsync(DisplayImage, preview);
        PlaceholderText.Visibility = Visibility.Collapsed;
        InfoOverlay.Visibility = Visibility.Visible;
        InfoText.Text = $"{preview.Width} x {preview.Height}  {preview.PixelFormat}";
        RedrawOverlays();
    }

    public void SetResults(IReadOnlyList<ClassificationResultDto> results)
    {
        _results = results;
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
