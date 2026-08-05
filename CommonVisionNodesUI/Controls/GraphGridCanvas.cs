using SkiaSharp;
using Uno.WinUI.Graphics2DSK;
using Windows.Foundation;

namespace CommonVisionNodesUI.Controls;

/// <summary>
/// Draws the graph editor background grid as one retained Skia surface.
/// </summary>
public sealed class GraphGridCanvas : SKCanvasElement
{
	private const double MinorGridSpacing = 25;
	private const double MajorGridSpacing = 100;

	private readonly SKPaint _minorGridPaint = new()
	{
		Color = new SKColor(40, 40, 40),
		StrokeWidth = 1,
		IsAntialias = false
	};
	private readonly SKPaint _majorGridPaint = new()
	{
		Color = new SKColor(50, 50, 50),
		StrokeWidth = 1,
		IsAntialias = false
	};
	private readonly SKPaint _originGridPaint = new()
	{
		Color = new SKColor(65, 65, 65),
		StrokeWidth = 1,
		IsAntialias = false
	};

	private double _scale = 1;
	private double _translateX;
	private double _translateY;
	private bool _hasViewTransform;

	/// <summary>
	/// Applies the graph canvas transform used to place grid lines in viewport coordinates.
	/// </summary>
	public void SetViewTransform(double scale, double translateX, double translateY)
	{
		if (_hasViewTransform && _scale == scale && _translateX == translateX && _translateY == translateY)
			return;

		_hasViewTransform = true;
		_scale = scale;
		_translateX = translateX;
		_translateY = translateY;
		Invalidate();
	}

	/// <inheritdoc />
	protected override void RenderOverride(SKCanvas canvas, Size area)
	{
		if (area.Width <= 0 || area.Height <= 0 || _scale <= 0)
			return;

		var showMinorGrid = MinorGridSpacing * _scale >= 6;
		var spacing = showMinorGrid ? MinorGridSpacing : MajorGridSpacing;
		var canvasLeft = -_translateX / _scale;
		var canvasTop = -_translateY / _scale;
		var canvasRight = (area.Width - _translateX) / _scale;
		var canvasBottom = (area.Height - _translateY) / _scale;

		var startX = Math.Floor(canvasLeft / spacing) * spacing;
		for (var canvasX = startX; canvasX <= canvasRight; canvasX += spacing)
		{
			var screenX = (float)(canvasX * _scale + _translateX);
			canvas.DrawLine(screenX, 0, screenX, (float)area.Height, GetGridPaint(canvasX, showMinorGrid));
		}

		var startY = Math.Floor(canvasTop / spacing) * spacing;
		for (var canvasY = startY; canvasY <= canvasBottom; canvasY += spacing)
		{
			var screenY = (float)(canvasY * _scale + _translateY);
			canvas.DrawLine(0, screenY, (float)area.Width, screenY, GetGridPaint(canvasY, showMinorGrid));
		}
	}

	private SKPaint GetGridPaint(double canvasCoordinate, bool showMinorGrid)
	{
		if (Math.Abs(canvasCoordinate) < 0.5)
			return _originGridPaint;

		return !showMinorGrid || Math.Abs(canvasCoordinate % MajorGridSpacing) < 0.5
			? _majorGridPaint
			: _minorGridPaint;
	}
}
