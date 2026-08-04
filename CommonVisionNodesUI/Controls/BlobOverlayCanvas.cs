using CommonVisionNodes.Contracts;
using SkiaSharp;
using Uno.WinUI.Graphics2DSK;
using Windows.Foundation;

namespace CommonVisionNodesUI.Controls;

/// <summary>
/// Draws all blob bounds as one batched Skia path instead of creating one XAML element per blob.
/// </summary>
public sealed class BlobOverlayCanvas : SKCanvasElement
{
    private readonly SKPath _blobPath = new();
    private readonly SKPaint _fillPaint = new()
    {
        Color = new SKColor(0, 255, 100, 40),
        IsAntialias = true,
        Style = SKPaintStyle.Fill
    };
    private readonly SKPaint _strokePaint = new()
    {
        Color = new SKColor(0, 255, 100),
        IsAntialias = true,
        Style = SKPaintStyle.Stroke
    };
    private int _sourceWidth;
    private int _sourceHeight;

    /// <summary>
    /// Rebuilds the single overlay path from the selected blobs.
    /// </summary>
    public void SetBlobs(IReadOnlyList<BlobInfoDto> blobs)
    {
        ArgumentNullException.ThrowIfNull(blobs);

        _blobPath.Reset();
        foreach (var blob in blobs)
        {
            _blobPath.AddRect(SKRect.Create(
                blob.BoundsX,
                blob.BoundsY,
                Math.Max(1, blob.BoundsWidth),
                Math.Max(1, blob.BoundsHeight)));
        }

        Invalidate();
    }

    /// <summary>
    /// Updates the source coordinate system used to align the path with the uniformly scaled image.
    /// </summary>
    public void SetSourceSize(int width, int height)
    {
        if (_sourceWidth == width && _sourceHeight == height)
            return;

        _sourceWidth = width;
        _sourceHeight = height;
        Invalidate();
    }

    /// <inheritdoc />
    protected override void RenderOverride(SKCanvas canvas, Size area)
    {
        if (_sourceWidth <= 0 || _sourceHeight <= 0 ||
            _blobPath.IsEmpty || area.Width <= 0 || area.Height <= 0)
        {
            return;
        }

        var scale = (float)Math.Min(area.Width / _sourceWidth, area.Height / _sourceHeight);
        var renderedWidth = _sourceWidth * scale;
        var renderedHeight = _sourceHeight * scale;
        var left = ((float)area.Width - renderedWidth) / 2;
        var top = ((float)area.Height - renderedHeight) / 2;
        var imageBounds = SKRect.Create(left, top, renderedWidth, renderedHeight);

        canvas.Save();
        canvas.ClipRect(imageBounds, SKClipOperation.Intersect, antialias: false);
        canvas.Translate(left, top);
        canvas.Scale(scale);

        _strokePaint.StrokeWidth = 1.5f / scale;
        canvas.DrawPath(_blobPath, _fillPaint);
        canvas.DrawPath(_blobPath, _strokePaint);
        canvas.Restore();
    }
}
