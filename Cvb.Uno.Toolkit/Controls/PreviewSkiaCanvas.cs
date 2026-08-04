using CommonVisionNodes.Contracts;
using Cvb.Uno.Toolkit.Helpers;
using SkiaSharp;
using Uno.WinUI.Graphics2DSK;
using Windows.Foundation;

namespace Cvb.Uno.Toolkit.Controls;

/// <summary>
/// Draws raw preview frames directly into Uno's hardware-accelerated Skia canvas.
/// </summary>
public sealed class PreviewSkiaCanvas : SKCanvasElement
{
	private SKImage? _image;
	private byte[]? _expandedBgraBuffer;

	/// <summary>
	/// Replaces the current frame. The raw payload is copied before this method returns.
	/// </summary>
	public void SetImage(ImagePreviewDto preview, byte[] bytes)
	{
		var nextImage = PreviewSkiaImageFactory.Create(preview, bytes, ref _expandedBgraBuffer);
		var oldImage = _image;
		_image = nextImage;
		oldImage?.Dispose();
		Invalidate();
	}

	/// <summary>
	/// Drops the current frame.
	/// </summary>
	public void ClearImage()
	{
		var oldImage = _image;
		_image = null;
		oldImage?.Dispose();
		Invalidate();
	}

	/// <inheritdoc />
	protected override void RenderOverride(SKCanvas canvas, Size area)
	{
		canvas.Clear(SKColors.Transparent);

		var image = _image;
		if (image is null || area.Width <= 0 || area.Height <= 0)
			return;

		var scale = Math.Min(area.Width / image.Width, area.Height / image.Height);
		var width = (float)(image.Width * scale);
		var height = (float)(image.Height * scale);
		var left = (float)((area.Width - width) / 2);
		var top = (float)((area.Height - height) / 2);
		var destination = SKRect.Create(left, top, width, height);

		canvas.DrawImage(image, destination, new SKSamplingOptions(SKFilterMode.Linear));
	}
}
