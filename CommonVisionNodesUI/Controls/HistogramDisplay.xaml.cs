using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;

namespace CommonVisionNodesUI.Controls;

/// <summary>
/// Renders a histogram as vertical bars with statistics overlay.
/// </summary>
public sealed partial class HistogramDisplay : UserControl
{
    public HistogramDisplay()
    {
        this.InitializeComponent();
        SizeChanged += (_, _) => Redraw();
    }

    private long[]? _bins;
    private double _mean;
    private double _stdDev;

    /// <summary>
    /// Updates the displayed histogram data.
    /// </summary>
    /// <param name="bins">Histogram bin values (typically 256 entries).</param>
    /// <param name="mean">Mean intensity.</param>
    /// <param name="stdDev">Standard deviation.</param>
    public void SetHistogram(long[]? bins, double mean, double stdDev)
    {
        _bins = bins;
        _mean = mean;
        _stdDev = stdDev;
        Redraw();
    }

    private void Redraw()
    {
        HistogramCanvas.Children.Clear();

        if (_bins == null || _bins.Length == 0)
        {
            PlaceholderText.Visibility = Visibility.Visible;
            StatsOverlay.Visibility = Visibility.Collapsed;
            return;
        }

        PlaceholderText.Visibility = Visibility.Collapsed;
        StatsOverlay.Visibility = Visibility.Visible;
        StatsText.Text = $"\u03BC {_mean:F1}  \u03C3 {_stdDev:F1}";

        double canvasW = HistogramCanvas.ActualWidth;
        double canvasH = HistogramCanvas.ActualHeight;
        if (canvasW <= 0 || canvasH <= 0) return;

        int binCount = _bins.Length;
        long maxBin = 0;
        for (int i = 0; i < binCount; i++)
        {
            if (_bins[i] > maxBin)
                maxBin = _bins[i];
        }
        if (maxBin == 0) return;

        double padding = 2;
        double drawW = canvasW - padding * 2;
        double drawH = canvasH - padding * 2;
        double barWidth = drawW / binCount;

        // Draw filled histogram using a single polygon
        var points = new PointCollection
        {
            new Point(padding, canvasH - padding)
        };

        for (int i = 0; i < binCount; i++)
        {
            double x = padding + i * barWidth;
            double h = _bins[i] / (double)maxBin * drawH;
            double y = canvasH - padding - h;
            points.Add(new Point(x, y));
            points.Add(new Point(x + barWidth, y));
        }

        points.Add(new Point(padding + drawW, canvasH - padding));

        var polygon = new Polygon
        {
            Points = points,
            Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(180, 100, 180, 255)),
            Stroke = new SolidColorBrush(Windows.UI.Color.FromArgb(220, 130, 200, 255)),
            StrokeThickness = 0.5
        };
        HistogramCanvas.Children.Add(polygon);

        // Mean indicator line
        double meanX = padding + (_mean / (binCount - 1)) * drawW;
        var meanLine = new Line
        {
            X1 = meanX, Y1 = padding,
            X2 = meanX, Y2 = canvasH - padding,
            Stroke = new SolidColorBrush(Windows.UI.Color.FromArgb(200, 255, 235, 59)),
            StrokeThickness = 1,
            StrokeDashArray = [3, 2]
        };
        HistogramCanvas.Children.Add(meanLine);
    }
}
