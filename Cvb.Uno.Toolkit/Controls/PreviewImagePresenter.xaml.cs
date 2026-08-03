using CommonVisionNodes.Contracts;
using Cvb.Uno.Toolkit.Helpers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Uno.WinUI.Graphics2DSK;

namespace Cvb.Uno.Toolkit.Controls;

/// <summary>
/// Presents raw previews on Uno's Skia canvas and encoded previews through the platform image decoder.
/// </summary>
public sealed partial class PreviewImagePresenter : UserControl
{
    private bool _isUsingRawCanvas;

    /// <summary>
    /// Creates an image presenter.
    /// </summary>
    public PreviewImagePresenter()
    {
        this.InitializeComponent();
    }

    /// <summary>
    /// Applies a preview, coalescing encoded image decodes and using the direct Skia path for binary raw frames.
    /// </summary>
    public async Task<ImagePreviewDto?> SetImageAsync(ImagePreviewDto? preview)
    {
        if (preview is null)
        {
            Clear();
            return null;
        }

        if (SKCanvasElement.IsSupportedOnCurrentPlatform() &&
            ImagePreviewEncodingInfo.IsRaw(preview.Encoding) &&
            preview.BinaryData is { Length: > 0 } bytes)
        {
            // Cancel a pending encoded decode before switching renderers. FromPixelCopy owns the
            // frame before this returns, so the WebSocket transport may safely reuse its buffer.
            if (!_isUsingRawCanvas)
                PreviewImageSourceLoader.ClearImage(FallbackImage);

            RawCanvas.SetImage(preview, bytes);
            FallbackImage.Visibility = Visibility.Collapsed;
            RawCanvas.Visibility = Visibility.Visible;
            _isUsingRawCanvas = true;
            return preview;
        }

        if (_isUsingRawCanvas)
        {
            RawCanvas.ClearImage();
            RawCanvas.Visibility = Visibility.Collapsed;
            _isUsingRawCanvas = false;
        }

        FallbackImage.Visibility = Visibility.Visible;
        return await PreviewImageSourceLoader.SetImageAsync(FallbackImage, preview);
    }

    /// <summary>
    /// Clears both rendering paths.
    /// </summary>
    public void Clear()
    {
        RawCanvas.ClearImage();
        PreviewImageSourceLoader.ClearImage(FallbackImage);
        RawCanvas.Visibility = Visibility.Collapsed;
        FallbackImage.Visibility = Visibility.Collapsed;
        _isUsingRawCanvas = false;
    }
}
