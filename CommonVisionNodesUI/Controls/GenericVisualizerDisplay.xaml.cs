using CommonVisionNodes;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using CvbImage = Stemmer.Cvb.Image;

namespace CommonVisionNodesUI.Controls;

/// <summary>
/// Displays any value received from a <see cref="GenericVisualizerNode"/>.
/// Automatically selects between an image preview and a string-list view
/// based on the runtime type of the value.
/// </summary>
public sealed partial class GenericVisualizerDisplay : UserControl
{
    public GenericVisualizerDisplay()
    {
        this.InitializeComponent();
    }

    /// <summary>
    /// Updates the visualization for the given value.
    /// </summary>
    /// <param name="value">The value to display.</param>
    public void SetValue(object? value)
    {
        switch (value)
        {
            case CvbImage img when !img.IsDisposed:
                ImageDisplay.SetImage(img);
                ImageDisplay.Visibility = Visibility.Visible;
                ListScroll.Visibility = Visibility.Collapsed;
                PlaceholderText.Visibility = Visibility.Collapsed;
                break;

            case IReadOnlyList<BlobInfo> blobs:
                ShowStringList(blobs.Select(b =>
                    $"#{b.Label}  area={b.Area}  " +
                    $"c=({b.CentroidX:F1},{b.CentroidY:F1})  " +
                    $"bounds=({b.BoundsX},{b.BoundsY}) {b.BoundsWidth}×{b.BoundsHeight}"));
                break;

            case IReadOnlyList<BlobRect> rects:
                ShowStringList(rects.Select((r, i) =>
                    $"#{i + 1}  ({r.X},{r.Y})  {r.Width}×{r.Height}"));
                break;

            case IReadOnlyList<PolimagoClassifyResultItem> results:
                ShowStringList(results.Select(r =>
                    $"{(r.BlobIndex >= 0 ? $"#{r.BlobIndex}" : "–")}  " +
                    $"{r.ClassName}  q={r.Quality:F3}  ({r.X:F0},{r.Y:F0})"));
                break;

            case null:
                ShowPlaceholder();
                break;

            default:
                ShowStringList([value.ToString() ?? value.GetType().Name]);
                break;
        }
    }

    private void ShowStringList(IEnumerable<string> items)
    {
        ItemsList.ItemsSource = items.ToList();
        ImageDisplay.Visibility = Visibility.Collapsed;
        ListScroll.Visibility = Visibility.Visible;
        PlaceholderText.Visibility = Visibility.Collapsed;
        ImageDisplay.Clear();
    }

    private void ShowPlaceholder()
    {
        ImageDisplay.Visibility = Visibility.Collapsed;
        ListScroll.Visibility = Visibility.Collapsed;
        PlaceholderText.Visibility = Visibility.Visible;
        ImageDisplay.Clear();
    }
}
