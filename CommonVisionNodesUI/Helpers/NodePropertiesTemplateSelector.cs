using CommonVisionNodesUI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace CommonVisionNodesUI.Helpers;

/// <summary>
/// Selects the appropriate properties panel template based on the node view model type.
/// </summary>
public class NodePropertiesTemplateSelector : DataTemplateSelector
{
	/// <summary>Template for <see cref="ImageNodeViewModel"/>.</summary>
	public DataTemplate? ImageNodeTemplate { get; set; }
	/// <summary>Template for <see cref="SaveImageNodeViewModel"/>.</summary>
	public DataTemplate? SaveImageNodeTemplate { get; set; }
	/// <summary>Template for <see cref="GevServerNodeViewModel"/>.</summary>
	public DataTemplate? GevServerNodeTemplate { get; set; }
	/// <summary>Template for <see cref="DeviceNodeViewModel"/>.</summary>
	public DataTemplate? DeviceNodeTemplate { get; set; }
	/// <summary>Template for <see cref="BinarizeNodeViewModel"/>.</summary>
	public DataTemplate? BinarizeNodeTemplate { get; set; }
	/// <summary>Template for <see cref="SubImageNodeViewModel"/>.</summary>
	public DataTemplate? SubImageNodeTemplate { get; set; }
	/// <summary>Template for <see cref="MatrixTransformNodeViewModel"/>.</summary>
	public DataTemplate? MatrixTransformNodeTemplate { get; set; }
	/// <summary>Template for <see cref="ImageGeneratorNodeViewModel"/>.</summary>
	public DataTemplate? ImageGeneratorNodeTemplate { get; set; }
	/// <summary>Template for <see cref="TimeTriggerNodeViewModel"/>.</summary>
	public DataTemplate? TimeTriggerNodeTemplate { get; set; }
	/// <summary>Template for <see cref="ManualTriggerNodeViewModel"/>.</summary>
	public DataTemplate? ManualTriggerNodeTemplate { get; set; }
	/// <summary>Template for <see cref="FilterNodeViewModel"/>.</summary>
	public DataTemplate? FilterNodeTemplate { get; set; }
	/// <summary>Template for <see cref="HistogramNodeViewModel"/>.</summary>
	public DataTemplate? HistogramNodeTemplate { get; set; }
	/// <summary>Template for <see cref="MorphologyNodeViewModel"/>.</summary>
	public DataTemplate? MorphologyNodeTemplate { get; set; }
	/// <summary>Template for <see cref="BlobNodeViewModel"/>.</summary>
	public DataTemplate? BlobNodeTemplate { get; set; }
	/// <summary>Template for <see cref="NormalizeNodeViewModel"/>.</summary>
	public DataTemplate? NormalizeNodeTemplate { get; set; }
	/// <summary>Template for <see cref="MinosSearchNodeViewModel"/>.</summary>
	public DataTemplate? MinosSearchNodeTemplate { get; set; }
	/// <summary>Template for <see cref="PolimagoClassifyNodeViewModel"/>.</summary>
	public DataTemplate? PolimagoClassifyNodeTemplate { get; set; }
	/// <summary>Template for <see cref="CodeReaderNodeViewModel"/>.</summary>
	public DataTemplate? CodeReaderNodeTemplate { get; set; }
	/// <summary>Template for <see cref="GenericVisualizerNodeViewModel"/>.</summary>
	public DataTemplate? GenericVisualizerNodeTemplate { get; set; }
	/// <summary>Template for <see cref="CSharpNodeViewModel"/>.</summary>
	public DataTemplate? CSharpNodeTemplate { get; set; }

	/// <summary>
	/// Returns the data template that matches the given node view model type.
	/// </summary>
	/// <param name="item">The node view model instance.</param>
	/// <returns>The matching template, or <c>null</c> if no match is found.</returns>
	protected override DataTemplate? SelectTemplateCore(object item)
	{
		return item switch
		{
			ImageNodeViewModel => ImageNodeTemplate,
			SaveImageNodeViewModel => SaveImageNodeTemplate,
			GevServerNodeViewModel => GevServerNodeTemplate,
			DeviceNodeViewModel => DeviceNodeTemplate,
			BinarizeNodeViewModel => BinarizeNodeTemplate,
			SubImageNodeViewModel => SubImageNodeTemplate,
			MatrixTransformNodeViewModel => MatrixTransformNodeTemplate,
			ImageGeneratorNodeViewModel => ImageGeneratorNodeTemplate,
			TimeTriggerNodeViewModel => TimeTriggerNodeTemplate,
			ManualTriggerNodeViewModel => ManualTriggerNodeTemplate,
			FilterNodeViewModel => FilterNodeTemplate,
			HistogramNodeViewModel => HistogramNodeTemplate,
			MorphologyNodeViewModel => MorphologyNodeTemplate,
			BlobNodeViewModel => BlobNodeTemplate,
			NormalizeNodeViewModel => NormalizeNodeTemplate,
			MinosSearchNodeViewModel => MinosSearchNodeTemplate,
			PolimagoClassifyNodeViewModel => PolimagoClassifyNodeTemplate,
			CodeReaderNodeViewModel => CodeReaderNodeTemplate,
			GenericVisualizerNodeViewModel => GenericVisualizerNodeTemplate,
			CSharpNodeViewModel => CSharpNodeTemplate,
			_ => null
		};
	}
}
