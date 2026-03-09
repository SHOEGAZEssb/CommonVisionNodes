using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using CommonVisionNodes;
using CommonVisionNodesUI.ViewModels;

namespace CommonVisionNodesUI.Helpers;

/// <summary>
/// Serializable representation of a node graph.
/// </summary>
public sealed class NodeGraphData
{
    public List<NodeData> Nodes { get; set; } = [];
    public List<ConnectionData> Connections { get; set; } = [];
}

/// <summary>
/// Serializable representation of a single node.
/// </summary>
public sealed class NodeData
{
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public double X { get; set; }
    public double Y { get; set; }
    public Dictionary<string, JsonElement> Properties { get; set; } = [];
}

/// <summary>
/// Serializable representation of a connection between two ports.
/// </summary>
public sealed class ConnectionData
{
    public string OutputNodeId { get; set; } = string.Empty;
    public string OutputPortName { get; set; } = string.Empty;
    public string InputNodeId { get; set; } = string.Empty;
    public string InputPortName { get; set; } = string.Empty;
}

/// <summary>
/// Serializes and deserializes a <see cref="NodeGraphViewModel"/> to/from JSON.
/// Uses reflection on the domain <see cref="Node"/> types so that new node types
/// only need a one-line entry in <see cref="_nodeRegistry"/>.
/// </summary>
public static class NodeGraphSerializer
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Registry mapping each node type name to its factory pair.
    /// Adding a new node type requires only one line here.
    /// </summary>
    private static readonly Dictionary<string, (Func<Node> CreateNode, Func<Node, double, double, NodeViewModel> CreateVM)> _nodeRegistry = new()
    {
        [nameof(ImageNode)]            = (() => new ImageNode(),            (n, x, y) => new ImageNodeViewModel((ImageNode)n, x, y)),
        [nameof(SaveImageNode)]        = (() => new SaveImageNode(),        (n, x, y) => new SaveImageNodeViewModel((SaveImageNode)n, x, y)),
        [nameof(DeviceNode)]           = (() => new DeviceNode(),           (n, x, y) => new DeviceNodeViewModel((DeviceNode)n, x, y)),
        [nameof(BinarizeNode)]         = (() => new BinarizeNode(),         (n, x, y) => new BinarizeNodeViewModel((BinarizeNode)n, x, y)),
        [nameof(SubImageNode)]         = (() => new SubImageNode(),         (n, x, y) => new SubImageNodeViewModel((SubImageNode)n, x, y)),
        [nameof(MatrixTransformNode)]  = (() => new MatrixTransformNode(),  (n, x, y) => new MatrixTransformNodeViewModel((MatrixTransformNode)n, x, y)),
        [nameof(ImageGeneratorNode)]   = (() => new ImageGeneratorNode(),   (n, x, y) => new ImageGeneratorNodeViewModel((ImageGeneratorNode)n, x, y)),
        [nameof(FilterNode)]           = (() => new FilterNode(),           (n, x, y) => new FilterNodeViewModel((FilterNode)n, x, y)),
        [nameof(HistogramNode)]        = (() => new HistogramNode(),        (n, x, y) => new HistogramNodeViewModel((HistogramNode)n, x, y)),
        [nameof(MorphologyNode)]       = (() => new MorphologyNode(),       (n, x, y) => new MorphologyNodeViewModel((MorphologyNode)n, x, y)),
        [nameof(BlobNode)]             = (() => new BlobNode(),             (n, x, y) => new BlobNodeViewModel((BlobNode)n, x, y)),
        [nameof(NormalizeNode)]        = (() => new NormalizeNode(),        (n, x, y) => new NormalizeNodeViewModel((NormalizeNode)n, x, y)),
        [nameof(PolimagoClassifyNode)] = (() => new PolimagoClassifyNode(), (n, x, y) => new PolimagoClassifyNodeViewModel((PolimagoClassifyNode)n, x, y)),
    };

    /// <summary>
    /// Serializes the current graph state to a JSON string.
    /// </summary>
    public static string Serialize(NodeGraphViewModel graph)
    {
        var data = new NodeGraphData();

        foreach (var nodeVM in graph.Nodes)
        {
            var nodeData = new NodeData
            {
                Id = nodeVM.Node.Id.ToString(),
                Type = nodeVM.Node.GetType().Name,
                X = nodeVM.X,
                Y = nodeVM.Y,
                Properties = GetNodeProperties(nodeVM.Node)
            };
            data.Nodes.Add(nodeData);
        }

        foreach (var conn in graph.Connections)
        {
            data.Connections.Add(new ConnectionData
            {
                OutputNodeId = conn.Connection.Output.Node.Id.ToString(),
                OutputPortName = conn.Connection.Output.Name,
                InputNodeId = conn.Connection.Input.Node.Id.ToString(),
                InputPortName = conn.Connection.Input.Name
            });
        }

        return JsonSerializer.Serialize(data, _jsonOptions);
    }

    /// <summary>
    /// Deserializes a JSON string and populates the graph with nodes and connections.
    /// The graph should be cleared before calling this method.
    /// </summary>
    public static void Deserialize(string json, NodeGraphViewModel graph)
    {
        var data = JsonSerializer.Deserialize<NodeGraphData>(json, _jsonOptions);
        if (data is null)
            return;

        var nodeMap = new Dictionary<string, (Node Node, NodeViewModel VM)>();

        foreach (var nodeData in data.Nodes)
        {
            if (!_nodeRegistry.TryGetValue(nodeData.Type, out var entry))
                continue;

            var node = entry.CreateNode();
            SetNodeProperties(node, nodeData.Properties);
            var vm = entry.CreateVM(node, nodeData.X, nodeData.Y);

            nodeMap[nodeData.Id] = (node, vm);
            graph.AddLoadedNode(node, vm);
        }

        foreach (var connData in data.Connections)
        {
            if (!nodeMap.TryGetValue(connData.OutputNodeId, out var source) ||
                !nodeMap.TryGetValue(connData.InputNodeId, out var target))
                continue;

            var outputPort = source.VM.OutputPorts.FirstOrDefault(p => p.Port.Name == connData.OutputPortName);
            var inputPort = target.VM.InputPorts.FirstOrDefault(p => p.Port.Name == connData.InputPortName);

            if (outputPort is not null && inputPort is not null)
                graph.TryConnect(outputPort, inputPort);
        }
    }

    /// <summary>
    /// Reflects over public read/write properties declared on the concrete node type
    /// (excluding <see cref="Port"/> members and inherited <see cref="Node"/> members).
    /// </summary>
    private static Dictionary<string, JsonElement> GetNodeProperties(Node node)
    {
        var dict = new Dictionary<string, object?>();
        foreach (var prop in GetSerializableProperties(node.GetType()))
        {
            var value = prop.GetValue(node);
            dict[prop.Name] = prop.PropertyType.IsEnum ? value?.ToString() : value;
        }

        var json = JsonSerializer.Serialize(dict, _jsonOptions);
        return JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json, _jsonOptions) ?? [];
    }

    /// <summary>
    /// Sets properties on a freshly-created node from deserialized JSON values.
    /// </summary>
    private static void SetNodeProperties(Node node, Dictionary<string, JsonElement> props)
    {
        foreach (var prop in GetSerializableProperties(node.GetType()))
        {
            if (!props.TryGetValue(prop.Name, out var element))
                continue;

            var value = DeserializeValue(element, prop.PropertyType);
            if (value is not null)
                prop.SetValue(node, value);
        }
    }

    /// <summary>
    /// Returns public instance properties that are declared on <paramref name="nodeType"/>
    /// (not inherited from <see cref="Node"/>), have a public setter, and are not <see cref="Port"/>s.
    /// </summary>
    private static IEnumerable<PropertyInfo> GetSerializableProperties(Type nodeType)
        => nodeType.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(p => p.PropertyType != typeof(Port) && p.CanRead && p.CanWrite && p.GetSetMethod() is not null);

    private static object? DeserializeValue(JsonElement element, Type targetType)
    {
        if (targetType.IsEnum)
            return Enum.TryParse(targetType, element.GetString(), out var e) ? e : null;

        if (targetType == typeof(string))  return element.GetString();
        if (targetType == typeof(int))     return element.GetInt32();
        if (targetType == typeof(double))  return element.GetDouble();
        if (targetType == typeof(float))   return element.GetSingle();
        if (targetType == typeof(bool))    return element.GetBoolean();
        if (targetType == typeof(long))    return element.GetInt64();

        return null;
    }
}
