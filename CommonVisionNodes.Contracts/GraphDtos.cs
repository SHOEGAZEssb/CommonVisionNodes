namespace CommonVisionNodes.Contracts;

/// <summary>
/// Identifies whether a port consumes or produces values.
/// </summary>
public enum PortDirectionDto
{
    /// <summary>
    /// Port receives a value from another node.
    /// </summary>
    Input,

    /// <summary>
    /// Port publishes a value to another node.
    /// </summary>
    Output
}

/// <summary>
/// Describes the editor and serialization type for a node property value.
/// </summary>
public enum NodePropertyValueKindDto
{
    /// <summary>
    /// Single-line text value.
    /// </summary>
    String,

    /// <summary>
    /// Integer numeric value.
    /// </summary>
    Integer,

    /// <summary>
    /// Floating-point numeric value.
    /// </summary>
    Double,

    /// <summary>
    /// Boolean value.
    /// </summary>
    Boolean,

    /// <summary>
    /// Choice selected from an option list.
    /// </summary>
    Enum,

    /// <summary>
    /// Multi-line text value, typically used for script/code bodies.
    /// </summary>
    MultilineText
}

/// <summary>
/// Describes the preview family a node can publish.
/// </summary>
public enum NodePreviewKindDto
{
    /// <summary>
    /// The node does not produce a preview.
    /// </summary>
    None,

    /// <summary>
    /// The node publishes image previews.
    /// </summary>
    Image,

    /// <summary>
    /// The node publishes histogram previews.
    /// </summary>
    Histogram,

    /// <summary>
    /// The node publishes blob overlay previews.
    /// </summary>
    Blob,

    /// <summary>
    /// The node publishes classification overlay previews.
    /// </summary>
    Classification,

    /// <summary>
    /// The node publishes CodeReader code-corner overlay previews.
    /// </summary>
    CodeReader,

    /// <summary>
    /// The node publishes text previews.
    /// </summary>
    Text
}

/// <summary>
/// Serializable graph definition exchanged between UI and backend.
/// </summary>
public sealed class GraphDto
{
    /// <summary>
    /// Nodes contained in the graph.
    /// </summary>
    public IList<NodeDto> Nodes { get; set; } = [];

    /// <summary>
    /// Directed connections between node ports.
    /// </summary>
    public IList<ConnectionDto> Connections { get; set; } = [];
}

/// <summary>
/// Serializable node instance within a graph.
/// </summary>
public sealed class NodeDto
{
    /// <summary>
    /// Stable graph-local identifier for this node instance.
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// Runtime node type name, such as <c>ImageNode</c>.
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Horizontal canvas position in UI units.
    /// </summary>
    public double X { get; set; }

    /// <summary>
    /// Vertical canvas position in UI units.
    /// </summary>
    public double Y { get; set; }

    /// <summary>
    /// Node width in UI units. A value of 0 lets the UI use its default width.
    /// </summary>
    public double Width { get; set; }

    /// <summary>
    /// Node height in UI units. A value of 0 lets the UI use its default height.
    /// </summary>
    public double Height { get; set; }

    /// <summary>
    /// Serialized property values for the node.
    /// </summary>
    public IList<NodePropertyDto> Properties { get; set; } = [];
}

/// <summary>
/// Describes a port exposed by a node definition.
/// </summary>
public sealed class PortDto
{
    /// <summary>
    /// Port display and lookup name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable type label used by the UI and connection validation.
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Input/output direction of the port.
    /// </summary>
    public PortDirectionDto Direction { get; set; }

    /// <summary>
    /// Human-readable explanation of the value carried by the port.
    /// </summary>
    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// Serializable directed connection from an output port to an input port.
/// </summary>
public sealed class ConnectionDto
{
    /// <summary>
    /// Node id that owns the source output port.
    /// </summary>
    public string OutputNodeId { get; set; } = string.Empty;

    /// <summary>
    /// Source output port name.
    /// </summary>
    public string OutputPortName { get; set; } = string.Empty;

    /// <summary>
    /// Node id that owns the target input port.
    /// </summary>
    public string InputNodeId { get; set; } = string.Empty;

    /// <summary>
    /// Target input port name.
    /// </summary>
    public string InputPortName { get; set; } = string.Empty;
}

/// <summary>
/// Serialized node property name/value pair.
/// </summary>
public sealed class NodePropertyDto
{
    /// <summary>
    /// Property name matching the runtime node property.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Serialized property value, using invariant-culture formatting where applicable.
    /// </summary>
    public string? Value { get; set; }
}

/// <summary>
/// Shared helper for the synthetic <c>ShowPreview</c> node property used by UI and backend.
/// </summary>
public static class NodePreviewSettings
{
    /// <summary>
    /// Property name used to store whether a node preview should be published.
    /// </summary>
    public const string ShowPreviewPropertyName = "ShowPreview";

    /// <summary>
    /// Resolves preview visibility for a node from its serialized properties.
    /// </summary>
    /// <param name="nodeType">Runtime node type name.</param>
    /// <param name="properties">Serialized node properties.</param>
    /// <returns><c>true</c> when preview publication is enabled.</returns>
    public static bool IsEnabled(string nodeType, IEnumerable<NodePropertyDto> properties)
    {
        ArgumentNullException.ThrowIfNull(nodeType);
        ArgumentNullException.ThrowIfNull(properties);

        foreach (var property in properties)
        {
            if (!string.Equals(property.Name, ShowPreviewPropertyName, StringComparison.OrdinalIgnoreCase))
                continue;

            if (bool.TryParse(property.Value, out var enabled))
                return enabled;
        }

        return IsEnabledByDefault(nodeType);
    }

    /// <summary>
    /// Returns the preview default for a node type when no explicit property is present.
    /// </summary>
    /// <param name="nodeType">Runtime node type name.</param>
    /// <returns><c>true</c> when previews should be enabled by default.</returns>
    public static bool IsEnabledByDefault(string nodeType)
        => string.Equals(nodeType, "GenericVisualizerNode", StringComparison.OrdinalIgnoreCase)
            || string.Equals(nodeType, "CodeReaderNode", StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Metadata describing a node type available in the runtime catalog.
/// </summary>
public sealed class NodeDefinitionDto
{
    /// <summary>
    /// Runtime node type name.
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// User-facing node name.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Palette/category name used by the UI.
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// User-facing explanation of the node.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Optional icon glyph used by the UI.
    /// </summary>
    public string? IconGlyph { get; set; }

    /// <summary>
    /// Preview family produced by this node.
    /// </summary>
    public NodePreviewKindDto PreviewKind { get; set; }

    /// <summary>
    /// Indicates whether node properties can be changed while continuous execution is running.
    /// </summary>
    public bool CanEditWhileRunning { get; set; }

    /// <summary>
    /// Input ports exposed by the node type.
    /// </summary>
    public IList<PortDto> InputPorts { get; set; } = [];

    /// <summary>
    /// Output ports exposed by the node type.
    /// </summary>
    public IList<PortDto> OutputPorts { get; set; } = [];

    /// <summary>
    /// Editable properties exposed by the node type.
    /// </summary>
    public IList<NodePropertyDefinitionDto> Properties { get; set; } = [];
}

/// <summary>
/// Metadata describing one editable node property.
/// </summary>
public sealed class NodePropertyDefinitionDto
{
    /// <summary>
    /// Runtime property name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// User-facing property label.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// User-facing property description.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Value kind used to select the editor control and parser.
    /// </summary>
    public NodePropertyValueKindDto ValueKind { get; set; }

    /// <summary>
    /// Default serialized value for new node instances.
    /// </summary>
    public string? DefaultValue { get; set; }

    /// <summary>
    /// Indicates whether the UI should display the property as read-only.
    /// </summary>
    public bool IsReadOnly { get; set; }

    /// <summary>
    /// Optional numeric minimum.
    /// </summary>
    public double? Minimum { get; set; }

    /// <summary>
    /// Optional numeric maximum.
    /// </summary>
    public double? Maximum { get; set; }

    /// <summary>
    /// Optional numeric step size.
    /// </summary>
    public double? Step { get; set; }

    /// <summary>
    /// Option list used by enum-like properties.
    /// </summary>
    public IList<PropertyOptionDto> Options { get; set; } = [];
}

/// <summary>
/// One selectable option for an enum-like property.
/// </summary>
public sealed class PropertyOptionDto
{
    /// <summary>
    /// Serialized option value.
    /// </summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// User-facing option label.
    /// </summary>
    public string Label { get; set; } = string.Empty;
}
