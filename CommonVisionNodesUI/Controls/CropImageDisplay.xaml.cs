using CommonVisionNodesUI.Helpers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using CvbImage = Stemmer.Cvb.Image;

namespace CommonVisionNodesUI.Controls;

/// <summary>
/// Displays a CVB image with an interactive crop rectangle overlay.
/// Users can drag a rectangle to define a crop area.
/// </summary>
public sealed partial class CropImageDisplay : UserControl
{
    private CvbImage? _currentImage;
    private bool _isDragging;
    private Windows.Foundation.Point _dragStart;

    private int _cropX, _cropY, _cropW, _cropH;
    private bool _hasCrop;

    /// <summary>
    /// Raised when the user finishes dragging a new crop rectangle.
    /// Parameters are (x, y, width, height) in image coordinates.
    /// </summary>
    public event Action<int, int, int, int>? CropAreaChanged;

    public CropImageDisplay()
    {
        this.InitializeComponent();
        this.SizeChanged += (_, _) => RedrawCropOverlay();
    }

    /// <summary>
    /// Updates the displayed image and redraws the crop overlay.
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
            CropRect.Visibility = Visibility.Collapsed;
            return;
        }

        DisplayImage.Source = CvbImageConverter.ConvertToWriteableBitmap(cvbImage);
        PlaceholderText.Visibility = Visibility.Collapsed;
        InfoOverlay.Visibility = Visibility.Visible;

        var channels = cvbImage.Planes.Count == 1 ? "Mono" : $"{cvbImage.Planes.Count}ch";
        var bpp = cvbImage.Planes[0].DataType.BitsPerPixel;
        InfoText.Text = $"{cvbImage.Width} \u00D7 {cvbImage.Height}  {channels}  {bpp}bpp";

        RedrawCropOverlay();
    }

    /// <summary>
    /// Sets the crop rectangle position and size in image coordinates and redraws it.
    /// </summary>
    /// <param name="x">X origin in pixels.</param>
    /// <param name="y">Y origin in pixels.</param>
    /// <param name="w">Width in pixels.</param>
    /// <param name="h">Height in pixels.</param>
    public void UpdateCropOverlay(int x, int y, int w, int h)
    {
        _cropX = x;
        _cropY = y;
        _cropW = w;
        _cropH = h;
        _hasCrop = true;
        RedrawCropOverlay();
    }

    private void RedrawCropOverlay()
    {
        if (!_hasCrop || _currentImage is null || ActualWidth <= 0 || ActualHeight <= 0)
        {
            CropRect.Visibility = Visibility.Collapsed;
            return;
        }

        var mapping = GetImageMapping();
        if (mapping.scaleX <= 0) return;

        double displayX = mapping.offsetX + _cropX / mapping.scaleX;
        double displayY = mapping.offsetY + _cropY / mapping.scaleY;
        double displayW = _cropW / mapping.scaleX;
        double displayH = _cropH / mapping.scaleY;

        Canvas.SetLeft(CropRect, displayX);
        Canvas.SetTop(CropRect, displayY);
        CropRect.Width = Math.Max(1, displayW);
        CropRect.Height = Math.Max(1, displayH);
        CropRect.Visibility = Visibility.Visible;
    }

    private (double offsetX, double offsetY, double scaleX, double scaleY) GetImageMapping()
    {
        if (_currentImage is null || ActualWidth <= 0 || ActualHeight <= 0)
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

    private void Overlay_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (_currentImage is null) return;
        _isDragging = true;
        _dragStart = e.GetCurrentPoint(OverlayCanvas).Position;
        OverlayCanvas.CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private void Overlay_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_isDragging || _currentImage is null) return;

        var current = e.GetCurrentPoint(OverlayCanvas).Position;
        UpdateDisplayRect(_dragStart, current);
        e.Handled = true;
    }

    private void Overlay_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (!_isDragging) return;
        _isDragging = false;
        OverlayCanvas.ReleasePointerCapture(e.Pointer);

        if (_currentImage is null) return;

        var current = e.GetCurrentPoint(OverlayCanvas).Position;
        var (imgX, imgY, imgW, imgH) = DisplayRectToImageRect(_dragStart, current);

        if (imgW > 0 && imgH > 0)
        {
            _cropX = imgX;
            _cropY = imgY;
            _cropW = imgW;
            _cropH = imgH;
            _hasCrop = true;
            CropAreaChanged?.Invoke(imgX, imgY, imgW, imgH);
        }

        e.Handled = true;
    }

    private void UpdateDisplayRect(Windows.Foundation.Point start, Windows.Foundation.Point end)
    {
        double x = Math.Min(start.X, end.X);
        double y = Math.Min(start.Y, end.Y);
        double w = Math.Abs(end.X - start.X);
        double h = Math.Abs(end.Y - start.Y);

        Canvas.SetLeft(CropRect, x);
        Canvas.SetTop(CropRect, y);
        CropRect.Width = Math.Max(1, w);
        CropRect.Height = Math.Max(1, h);
        CropRect.Visibility = Visibility.Visible;
    }

    private (int x, int y, int w, int h) DisplayRectToImageRect(
        Windows.Foundation.Point start, Windows.Foundation.Point end)
    {
        var mapping = GetImageMapping();
        if (mapping.scaleX <= 0 || _currentImage is null)
            return (0, 0, 0, 0);

        double displayX = Math.Min(start.X, end.X);
        double displayY = Math.Min(start.Y, end.Y);
        double displayW = Math.Abs(end.X - start.X);
        double displayH = Math.Abs(end.Y - start.Y);

        int imgX = (int)Math.Round((displayX - mapping.offsetX) * mapping.scaleX);
        int imgY = (int)Math.Round((displayY - mapping.offsetY) * mapping.scaleY);
        int imgW = (int)Math.Round(displayW * mapping.scaleX);
        int imgH = (int)Math.Round(displayH * mapping.scaleY);

        imgX = Math.Clamp(imgX, 0, _currentImage.Width - 1);
        imgY = Math.Clamp(imgY, 0, _currentImage.Height - 1);
        imgW = Math.Clamp(imgW, 1, _currentImage.Width - imgX);
        imgH = Math.Clamp(imgH, 1, _currentImage.Height - imgY);

        return (imgX, imgY, imgW, imgH);
    }
}
