using CommonVisionNodes.Contracts;
using Cvb.Uno.Toolkit.Helpers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Cvb.Uno.Toolkit.Controls;

/// <summary>
/// Displays an image preview produced by a CVB-backed source.
/// </summary>
public sealed partial class CvbImageDisplay : UserControl
{
	/// <summary>
	/// Creates the image preview control.
	/// </summary>
	public CvbImageDisplay()
	{
		this.InitializeComponent();
	}

	/// <summary>
	/// Updates the displayed image from a preview payload.
	/// </summary>
	/// <param name="preview">Preview payload, or <c>null</c> to clear the display.</param>
	public async void SetImage(ImagePreviewDto? preview)
	{
		if (preview is null)
		{
			Clear();
			return;
		}

		var appliedPreview = await DisplayImage.SetImageAsync(preview);
		if (appliedPreview is null)
			return;

		PlaceholderText.Visibility = Visibility.Collapsed;
		InfoOverlay.Visibility = Visibility.Visible;
		InfoText.Text = PreviewImageSourceLoader.GetPreviewInfoText(appliedPreview);
	}

	/// <summary>
	/// Clears the displayed image and restores the placeholder.
	/// </summary>
	public void Clear()
	{
		DisplayImage.Clear();
		PlaceholderText.Visibility = Visibility.Visible;
		InfoOverlay.Visibility = Visibility.Collapsed;
	}
}
