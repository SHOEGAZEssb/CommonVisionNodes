using CommonVisionNodesUI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace CommonVisionNodesUI.Helpers;

public class NodePropertiesTemplateSelector : DataTemplateSelector
{
    public DataTemplate? ImageNodeTemplate { get; set; }
    public DataTemplate? SaveImageNodeTemplate { get; set; }
    public DataTemplate? DeviceNodeTemplate { get; set; }
    public DataTemplate? BinarizeNodeTemplate { get; set; }
    public DataTemplate? SubImageNodeTemplate { get; set; }

    protected override DataTemplate? SelectTemplateCore(object item)
    {
        return item switch
        {
            ImageNodeViewModel => ImageNodeTemplate,
            SaveImageNodeViewModel => SaveImageNodeTemplate,
            DeviceNodeViewModel => DeviceNodeTemplate,
            BinarizeNodeViewModel => BinarizeNodeTemplate,
            SubImageNodeViewModel => SubImageNodeTemplate,
            _ => null
        };
    }
}
