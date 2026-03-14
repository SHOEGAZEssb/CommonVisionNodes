using CommonVisionNodes.Contracts;
using CommonVisionNodesUI.Helpers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;

namespace CommonVisionNodesUI.Controls;

public sealed partial class BlobImageDisplay : UserControl
{
    private ImagePreviewDto? _currentImage;
    private IReadOnlyList<BlobInfoDto> _blobs = [];

    public BlobImageDisplay()
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

    public void SetBlobs(IReadOnlyList<BlobInfoDto> blobs)
    {
        _blobs = blobs;
        RedrawOverlays();
    }

    private void RedrawOverlays()
    {
        OverlayCanvas.Children.Clear();

        if (_currentImage is null || _blobs.Count == 0 || ActualWidth <= 0 || ActualHeight <= 0)
            return;

        var mapping = GetImageMapping();
        if (mapping.scaleX <= 0)
            return;

        foreach (var blob in _blobs)
        {
            var displayX = mapping.offsetX + blob.BoundsX / mapping.scaleX;
            var displayY = mapping.offsetY + blob.BoundsY / mapping.scaleY;
            var displayW = blob.BoundsWidth / mapping.scaleX;
            var displayH = blob.BoundsHeight / mapping.scaleY;

            var rect = new Rectangle
            {
                Stroke = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0, 255, 100)),
                StrokeThickness = 1.5,
                Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(40, 0, 255, 100)),
                Width = Math.Max(1, displayW),
                Height = Math.Max(1, displayH)
            };

            Canvas.SetLeft(rect, displayX);
            Canvas.SetTop(rect, displayY);
            OverlayCanvas.Children.Add(rect);
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
