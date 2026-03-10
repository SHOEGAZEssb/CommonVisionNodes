using CommonVisionNodes;
using CommonVisionNodesUI.Helpers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using CvbImage = Stemmer.Cvb.Image;

namespace CommonVisionNodesUI.Controls;

/// <summary>
/// Displays a CVB image with Polimago classification result overlays.
/// Each result is rendered as a circle at the classified point with a class-name label.
/// </summary>
public sealed partial class PolimagoImageDisplay : UserControl
{
    private CvbImage? _currentImage;
    private IReadOnlyList<PolimagoClassifyResultItem> _results = [];

    public PolimagoImageDisplay()
    {
        this.InitializeComponent();
        this.SizeChanged += (_, _) => RedrawOverlays();
    }

    /// <summary>
    /// Updates the displayed image and redraws result overlays.
    /// </summary>
    /// <param name="cvbImage">The image to display, or <c>null</c> to show placeholder text.</param>
    public void SetImage(CvbImage? cvbImage)
    {
        _currentImage = cvbImage;

        if (cvbImage is null || cvbImage.IsDisposed)
        {
            DisplayImage.Source = null;
            PlaceholderText.Visibility = Visibility.Visible;
            InfoOverlay.Visibility = Visibility.Collapsed;
            OverlayCanvas.Children.Clear();
            return;
        }

        DisplayImage.Source = CvbImageConverter.ConvertToWriteableBitmap(cvbImage);
        PlaceholderText.Visibility = Visibility.Collapsed;
        InfoOverlay.Visibility = Visibility.Visible;

        var channels = cvbImage.Planes.Count == 1 ? "Mono" : $"{cvbImage.Planes.Count}ch";
        var bpp = cvbImage.Planes[0].DataType.BitsPerPixel;
        InfoText.Text = $"{cvbImage.Width} \u00D7 {cvbImage.Height}  {channels}  {bpp}bpp";

        RedrawOverlays();
    }

    /// <summary>
    /// Updates the result list and redraws overlays.
    /// </summary>
    /// <param name="results">The classification results to overlay.</param>
    public void SetResults(IReadOnlyList<PolimagoClassifyResultItem> results)
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
            double displayX = mapping.offsetX + result.X / mapping.scaleX;
            double displayY = mapping.offsetY + result.Y / mapping.scaleY;

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
        if (_currentImage is null || _currentImage.IsDisposed || ActualWidth <= 0 || ActualHeight <= 0)
            return (0, 0, 0, 0);

        double imgW = _currentImage.Width;
        double imgH = _currentImage.Height;
        double containerW = ActualWidth;
        double containerH = ActualHeight;

        double imgAspect = imgW / imgH;
        double containerAspect = containerW / containerH;

        double renderedW, renderedH, offsetX, offsetY;

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
