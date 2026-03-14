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

        await PreviewImageSourceLoader.SetImageAsync(DisplayImage, preview);
        PlaceholderText.Visibility = Visibility.Collapsed;
        InfoOverlay.Visibility = Visibility.Visible;
        InfoText.Text = $"{preview.Width} x {preview.Height}  {preview.PixelFormat}";
    }

    public void Clear()
    {
        DisplayImage.Source = null;
        PlaceholderText.Visibility = Visibility.Visible;
        InfoOverlay.Visibility = Visibility.Collapsed;
    }
}
