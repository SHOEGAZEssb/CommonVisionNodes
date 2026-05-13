using CommonVisionNodes.Contracts;
using CommonVisionNodesUI.Helpers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace CommonVisionNodesUI.Controls;

public sealed partial class CvbImageDisplay : UserControl
{
    public CvbImageDisplay()
    {
        this.InitializeComponent();
    }

    public async void SetImage(ImagePreviewDto? preview)
    {
        if (preview is null)
        {
            Clear();
            return;
        }

        if (!await PreviewImageSourceLoader.SetImageAsync(DisplayImage, preview))
            return;

        PlaceholderText.Visibility = Visibility.Collapsed;
        InfoOverlay.Visibility = Visibility.Visible;
        InfoText.Text = PreviewImageSourceLoader.GetPreviewInfoText(preview);
    }

    public void Clear()
    {
        DisplayImage.Source = null;
        PlaceholderText.Visibility = Visibility.Visible;
        InfoOverlay.Visibility = Visibility.Collapsed;
    }
}
