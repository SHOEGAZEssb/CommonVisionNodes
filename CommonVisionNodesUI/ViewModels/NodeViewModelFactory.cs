using CommonVisionNodes.Contracts;

namespace CommonVisionNodesUI.ViewModels;

/// <summary>
/// Creates specialized node view models from catalog definitions.
/// </summary>
public static class NodeViewModelFactory
{
    /// <summary>
    /// Creates the view model that matches a node definition type.
    /// </summary>
    /// <param name="node">Serialized node instance.</param>
    /// <param name="definition">Node catalog definition.</param>
    /// <param name="refreshDeviceDefinitionsAsync">Optional callback used by device nodes to refresh device options.</param>
    /// <returns>A specialized node view model.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the node type is unsupported.</exception>
    public static NodeViewModel Create(NodeDto node, NodeDefinitionDto definition, Func<Task>? refreshDeviceDefinitionsAsync = null)
    {
        return definition.Type switch
        {
            "ImageNode" => new ImageNodeViewModel(node, definition),
            "SaveImageNode" => new SaveImageNodeViewModel(node, definition),
            "GevServerNode" => new GevServerNodeViewModel(node, definition),
            "DeviceNode" => new DeviceNodeViewModel(node, definition, refreshDeviceDefinitionsAsync),
            "BinarizeNode" => new BinarizeNodeViewModel(node, definition),
            "SubImageNode" => new SubImageNodeViewModel(node, definition),
            "MatrixTransformNode" => new MatrixTransformNodeViewModel(node, definition),
            "ImageGeneratorNode" => new ImageGeneratorNodeViewModel(node, definition),
            "TimeTriggerNode" => new TimeTriggerNodeViewModel(node, definition),
            "ManualTriggerNode" => new ManualTriggerNodeViewModel(node, definition),
            "FilterNode" => new FilterNodeViewModel(node, definition),
            "HistogramNode" => new HistogramNodeViewModel(node, definition),
            "MorphologyNode" => new MorphologyNodeViewModel(node, definition),
            "BlobNode" => new BlobNodeViewModel(node, definition),
            "NormalizeNode" => new NormalizeNodeViewModel(node, definition),
            "MinosSearchNode" => new MinosSearchNodeViewModel(node, definition),
            "PolimagoClassifyNode" => new PolimagoClassifyNodeViewModel(node, definition),
            "CodeReaderNode" => new CodeReaderNodeViewModel(node, definition),
            "GenericVisualizerNode" => new GenericVisualizerNodeViewModel(node, definition),
            "CSharpNode" => new CSharpNodeViewModel(node, definition),
            _ => throw new InvalidOperationException($"Unsupported node type '{definition.Type}'.")
        };
    }
}
