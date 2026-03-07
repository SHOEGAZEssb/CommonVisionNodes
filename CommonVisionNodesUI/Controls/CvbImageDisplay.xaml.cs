using CommonVisionNodesUI.Helpers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using CvbImage = Stemmer.Cvb.Image;

namespace CommonVisionNodesUI.Controls;

public sealed partial class CvbImageDisplay : UserControl
{
    public CvbImageDisplay()
    {
        this.InitializeComponent();
    }

    public void SetImage(CvbImage? cvbImage)
    {
        if (cvbImage is null || cvbImage.IsDisposed)
        {
            DisplayImage.Source = null;
            PlaceholderText.Visibility = Visibility.Visible;
            InfoOverlay.Visibility = Visibility.Collapsed;
            return;
        }

        DisplayImage.Source = CvbImageConverter.ConvertToWriteableBitmap(cvbImage);
        PlaceholderText.Visibility = Visibility.Collapsed;
        InfoOverlay.Visibility = Visibility.Visible;

        var channels = cvbImage.Planes.Count == 1 ? "Mono" : $"{cvbImage.Planes.Count}ch";
        var bpp = cvbImage.Planes[0].DataType.BitsPerPixel;
        InfoText.Text = $"{cvbImage.Width} \u00D7 {cvbImage.Height}  {channels}  {bpp}bpp";
    }

    public void Clear()
    {
        DisplayImage.Source = null;
        PlaceholderText.Visibility = Visibility.Visible;
        InfoOverlay.Visibility = Visibility.Collapsed;
    }
}
