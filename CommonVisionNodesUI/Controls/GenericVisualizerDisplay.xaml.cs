using CommonVisionNodes.Contracts;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace CommonVisionNodesUI.Controls;

public sealed partial class GenericVisualizerDisplay : UserControl
{
    public GenericVisualizerDisplay()
    {
        this.InitializeComponent();
    }

    public void SetImagePreview(ImagePreviewDto? preview)
    {
        if (preview is null)
        {
            ImageDisplay.Visibility = Visibility.Collapsed;
            ListScroll.Visibility = Visibility.Collapsed;
            PlaceholderText.Visibility = Visibility.Visible;
            ImageDisplay.Clear();
            return;
        }

        ImageDisplay.SetImage(preview);
        ImageDisplay.Visibility = Visibility.Visible;
        ListScroll.Visibility = Visibility.Collapsed;
        PlaceholderText.Visibility = Visibility.Collapsed;
    }

    public void SetText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            ImageDisplay.Visibility = Visibility.Collapsed;
            ListScroll.Visibility = Visibility.Collapsed;
            PlaceholderText.Visibility = Visibility.Visible;
            ImageDisplay.Clear();
            return;
        }

        ItemsList.ItemsSource = text.Split([Environment.NewLine], StringSplitOptions.None).ToList();
        ImageDisplay.Visibility = Visibility.Collapsed;
        ListScroll.Visibility = Visibility.Visible;
        PlaceholderText.Visibility = Visibility.Collapsed;
        ImageDisplay.Clear();
    }
}
