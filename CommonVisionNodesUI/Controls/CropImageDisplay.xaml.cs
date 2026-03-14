using CommonVisionNodes.Contracts;
using CommonVisionNodesUI.Helpers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace CommonVisionNodesUI.Controls;

public sealed partial class CropImageDisplay : UserControl
{
    private ImagePreviewDto? _currentImage;
    private bool _isDragging;
    private Windows.Foundation.Point _dragStart;
    private int _cropX, _cropY, _cropW, _cropH;
    private bool _hasCrop;

    public event Action<int, int, int, int>? CropAreaChanged;

    public CropImageDisplay()
    {
        this.InitializeComponent();
        SizeChanged += (_, _) => RedrawCropOverlay();
    }

    public async void SetImage(ImagePreviewDto? preview)
    {
        _currentImage = preview;

        if (preview is null)
        {
            DisplayImage.Source = null;
            PlaceholderText.Visibility = Visibility.Visible;
            InfoOverlay.Visibility = Visibility.Collapsed;
            CropRect.Visibility = Visibility.Collapsed;
            return;
        }

        await PreviewImageSourceLoader.SetImageAsync(DisplayImage, preview);
        PlaceholderText.Visibility = Visibility.Collapsed;
        InfoOverlay.Visibility = Visibility.Visible;
        InfoText.Text = $"{preview.Width} x {preview.Height}  {preview.PixelFormat}";
        RedrawCropOverlay();
    }

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
        if (mapping.scaleX <= 0)
            return;

        var displayX = mapping.offsetX + _cropX / mapping.scaleX;
        var displayY = mapping.offsetY + _cropY / mapping.scaleY;
        var displayW = _cropW / mapping.scaleX;
        var displayH = _cropH / mapping.scaleY;

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

    private void Overlay_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (_currentImage is null)
            return;

        _isDragging = true;
        _dragStart = e.GetCurrentPoint(OverlayCanvas).Position;
        OverlayCanvas.CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private void Overlay_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_isDragging || _currentImage is null)
            return;

        var current = e.GetCurrentPoint(OverlayCanvas).Position;
        UpdateDisplayRect(_dragStart, current);
        e.Handled = true;
    }

    private void Overlay_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (!_isDragging)
            return;

        _isDragging = false;
        OverlayCanvas.ReleasePointerCapture(e.Pointer);

        if (_currentImage is null)
            return;

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
        var x = Math.Min(start.X, end.X);
        var y = Math.Min(start.Y, end.Y);
        var w = Math.Abs(end.X - start.X);
        var h = Math.Abs(end.Y - start.Y);

        Canvas.SetLeft(CropRect, x);
        Canvas.SetTop(CropRect, y);
        CropRect.Width = Math.Max(1, w);
        CropRect.Height = Math.Max(1, h);
        CropRect.Visibility = Visibility.Visible;
    }

    private (int x, int y, int w, int h) DisplayRectToImageRect(Windows.Foundation.Point start, Windows.Foundation.Point end)
    {
        var mapping = GetImageMapping();
        if (mapping.scaleX <= 0 || _currentImage is null)
            return (0, 0, 0, 0);

        var displayX = Math.Min(start.X, end.X);
        var displayY = Math.Min(start.Y, end.Y);
        var displayW = Math.Abs(end.X - start.X);
        var displayH = Math.Abs(end.Y - start.Y);

        var imgX = (int)Math.Round((displayX - mapping.offsetX) * mapping.scaleX);
        var imgY = (int)Math.Round((displayY - mapping.offsetY) * mapping.scaleY);
        var imgW = (int)Math.Round(displayW * mapping.scaleX);
        var imgH = (int)Math.Round(displayH * mapping.scaleY);

        imgX = Math.Clamp(imgX, 0, _currentImage.Width - 1);
        imgY = Math.Clamp(imgY, 0, _currentImage.Height - 1);
        imgW = Math.Clamp(imgW, 1, _currentImage.Width - imgX);
        imgH = Math.Clamp(imgH, 1, _currentImage.Height - imgY);

        return (imgX, imgY, imgW, imgH);
    }
}
