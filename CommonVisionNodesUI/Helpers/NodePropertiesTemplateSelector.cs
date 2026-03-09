using CommonVisionNodesUI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace CommonVisionNodesUI.Helpers;

/// <summary>
/// Selects the appropriate properties panel template based on the node view model type.
/// </summary>
public class NodePropertiesTemplateSelector : DataTemplateSelector
{
    public DataTemplate? ImageNodeTemplate { get; set; }
    public DataTemplate? SaveImageNodeTemplate { get; set; }
    public DataTemplate? DeviceNodeTemplate { get; set; }
    public DataTemplate? BinarizeNodeTemplate { get; set; }
    public DataTemplate? SubImageNodeTemplate { get; set; }
    public DataTemplate? MatrixTransformNodeTemplate { get; set; }
    public DataTemplate? ImageGeneratorNodeTemplate { get; set; }
    public DataTemplate? FilterNodeTemplate { get; set; }
    public DataTemplate? HistogramNodeTemplate { get; set; }
    public DataTemplate? MorphologyNodeTemplate { get; set; }
    public DataTemplate? BlobNodeTemplate { get; set; }
    public DataTemplate? NormalizeNodeTemplate { get; set; }
    public DataTemplate? PolimagoClassifyNodeTemplate { get; set; }
    public DataTemplate? GenericVisualizerNodeTemplate { get; set; }

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
            DeviceNodeViewModel => DeviceNodeTemplate,
            BinarizeNodeViewModel => BinarizeNodeTemplate,
            SubImageNodeViewModel => SubImageNodeTemplate,
            MatrixTransformNodeViewModel => MatrixTransformNodeTemplate,
            ImageGeneratorNodeViewModel => ImageGeneratorNodeTemplate,
            FilterNodeViewModel => FilterNodeTemplate,
            HistogramNodeViewModel => HistogramNodeTemplate,
            MorphologyNodeViewModel => MorphologyNodeTemplate,
            BlobNodeViewModel => BlobNodeTemplate,
            NormalizeNodeViewModel => NormalizeNodeTemplate,
            PolimagoClassifyNodeViewModel => PolimagoClassifyNodeTemplate,
            GenericVisualizerNodeViewModel => GenericVisualizerNodeTemplate,
            _ => null
        };
    }
}
