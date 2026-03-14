using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using CommonVisionNodes.Contracts;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage.Streams;

namespace CommonVisionNodesUI.Helpers;

internal static class PreviewImageSourceLoader
{
    public static async Task SetImageAsync(Image image, ImagePreviewDto? preview)
    {
        if (preview is null || string.IsNullOrWhiteSpace(preview.Base64Data))
        {
            image.Source = null;
            return;
        }

        var bytes = Convert.FromBase64String(preview.Base64Data);
        using var stream = new InMemoryRandomAccessStream();
        await stream.WriteAsync(bytes.AsBuffer());
        stream.Seek(0);

        var bitmap = new BitmapImage();
        await bitmap.SetSourceAsync(stream);
        image.Source = bitmap;
    }
}

