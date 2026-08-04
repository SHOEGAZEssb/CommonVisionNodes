using CommonVisionNodes.Contracts;
using Cvb.Uno.Toolkit.Helpers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace CommonVisionNodesUI.Controls;

/// <summary>
/// Displays an image preview with blob bounding-box overlays.
/// </summary>
public sealed partial class BlobImageDisplay : UserControl
{
	/// <summary>
	/// Creates the blob image display control.
	/// </summary>
	public BlobImageDisplay()
	{
		this.InitializeComponent();
	}

	/// <summary>
	/// Updates the image used behind blob overlays.
	/// </summary>
	/// <param name="preview">Preview payload, or <c>null</c> to clear the display.</param>
	public async void SetImage(ImagePreviewDto? preview)
	{
		if (preview is null)
		{
			DisplayImage.Clear();
			PlaceholderText.Visibility = Visibility.Visible;
			InfoOverlay.Visibility = Visibility.Collapsed;
			OverlayCanvas.SetSourceSize(0, 0);
			return;
		}

		var appliedPreview = await DisplayImage.SetImageAsync(preview);
		if (appliedPreview is null)
			return;

		PlaceholderText.Visibility = Visibility.Collapsed;
		InfoOverlay.Visibility = Visibility.Visible;
		InfoText.Text = PreviewImageSourceLoader.GetPreviewInfoText(appliedPreview);
		OverlayCanvas.SetSourceSize(appliedPreview.Width, appliedPreview.Height);
	}

	/// <summary>
	/// Updates blob overlays.
	/// </summary>
	/// <param name="blobs">Blob data to draw.</param>
	public void SetBlobs(IReadOnlyList<BlobInfoDto> blobs)
	{
		OverlayCanvas.SetBlobs(blobs);
	}
}
