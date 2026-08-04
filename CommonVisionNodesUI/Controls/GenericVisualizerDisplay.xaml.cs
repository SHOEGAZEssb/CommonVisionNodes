using CommonVisionNodes.Contracts;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace CommonVisionNodesUI.Controls;

/// <summary>
/// Displays generic visualizer content as either an image preview or a text list.
/// </summary>
public sealed partial class GenericVisualizerDisplay : UserControl
{
	/// <summary>
	/// Creates the generic visualizer display control.
	/// </summary>
	public GenericVisualizerDisplay()
	{
		this.InitializeComponent();
	}

	private void Root_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
	{
		// Keep wheel input inside the embedded visualizer so the graph canvas does not zoom or pan.
		e.Handled = true;
	}

	/// <summary>
	/// Shows an image preview and hides text content.
	/// </summary>
	/// <param name="preview">Preview payload, or <c>null</c> to clear the image.</param>
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

	/// <summary>
	/// Shows text content and hides image content.
	/// </summary>
	/// <param name="text">Text to display.</param>
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
