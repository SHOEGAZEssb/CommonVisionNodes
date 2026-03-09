using CommonVisionNodes;
using CommonVisionNodesUI.Helpers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using CvbImage = Stemmer.Cvb.Image;

namespace CommonVisionNodesUI.Controls;

/// <summary>
/// Displays a CVB image with blob bounding box overlays.
/// </summary>
public sealed partial class BlobImageDisplay : UserControl
{
    private CvbImage? _currentImage;
    private IReadOnlyList<BlobInfo> _blobs = [];

    public BlobImageDisplay()
    {
        this.InitializeComponent();
        this.SizeChanged += (_, _) => RedrawOverlays();
    }

    /// <summary>
    /// Updates the displayed image and redraws blob overlays.
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
    /// Updates the blob list and redraws overlays.
    /// </summary>
    /// <param name="blobs">The detected blobs to draw bounding boxes for.</param>
    public void SetBlobs(IReadOnlyList<BlobInfo> blobs)
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
            double displayX = mapping.offsetX + blob.BoundsX / mapping.scaleX;
            double displayY = mapping.offsetY + blob.BoundsY / mapping.scaleY;
            double displayW = blob.BoundsWidth / mapping.scaleX;
            double displayH = blob.BoundsHeight / mapping.scaleY;

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
