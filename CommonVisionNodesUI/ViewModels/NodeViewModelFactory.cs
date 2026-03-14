using CommonVisionNodes.Contracts;

namespace CommonVisionNodesUI.ViewModels;

public static class NodeViewModelFactory
{
    public static NodeViewModel Create(NodeDto node, NodeDefinitionDto definition, Func<Task>? refreshDeviceDefinitionsAsync = null)
    {
        return definition.Type switch
        {
            "ImageNode" => new ImageNodeViewModel(node, definition),
            "SaveImageNode" => new SaveImageNodeViewModel(node, definition),
            "DeviceNode" => new DeviceNodeViewModel(node, definition, refreshDeviceDefinitionsAsync),
            "BinarizeNode" => new BinarizeNodeViewModel(node, definition),
            "SubImageNode" => new SubImageNodeViewModel(node, definition),
            "MatrixTransformNode" => new MatrixTransformNodeViewModel(node, definition),
            "ImageGeneratorNode" => new ImageGeneratorNodeViewModel(node, definition),
            "FilterNode" => new FilterNodeViewModel(node, definition),
            "HistogramNode" => new HistogramNodeViewModel(node, definition),
            "MorphologyNode" => new MorphologyNodeViewModel(node, definition),
            "BlobNode" => new BlobNodeViewModel(node, definition),
            "NormalizeNode" => new NormalizeNodeViewModel(node, definition),
            "PolimagoClassifyNode" => new PolimagoClassifyNodeViewModel(node, definition),
            "GenericVisualizerNode" => new GenericVisualizerNodeViewModel(node, definition),
            "CSharpNode" => new CSharpNodeViewModel(node, definition),
            _ => throw new InvalidOperationException($"Unsupported node type '{definition.Type}'.")
        };
    }
}
