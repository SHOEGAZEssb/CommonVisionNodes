namespace CommonVisionNodes.Contracts;

public enum PortDirectionDto
{
    Input,
    Output
}

public enum NodePropertyValueKindDto
{
    String,
    Integer,
    Double,
    Boolean,
    Enum,
    MultilineText
}

public enum NodePreviewKindDto
{
    None,
    Image,
    Histogram,
    Blob,
    Classification,
    Text
}

public sealed class GraphDto
{
    public IList<NodeDto> Nodes { get; set; } = [];
    public IList<ConnectionDto> Connections { get; set; } = [];
}

public sealed class NodeDto
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Type { get; set; } = string.Empty;
    public double X { get; set; }
    public double Y { get; set; }
    public IList<NodePropertyDto> Properties { get; set; } = [];
}

public sealed class PortDto
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public PortDirectionDto Direction { get; set; }
    public string Description { get; set; } = string.Empty;
}

public sealed class ConnectionDto
{
    public string OutputNodeId { get; set; } = string.Empty;
    public string OutputPortName { get; set; } = string.Empty;
    public string InputNodeId { get; set; } = string.Empty;
    public string InputPortName { get; set; } = string.Empty;
}

public sealed class NodePropertyDto
{
    public string Name { get; set; } = string.Empty;
    public string? Value { get; set; }
}

public sealed class NodeDefinitionDto
{
    public string Type { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? IconGlyph { get; set; }
    public NodePreviewKindDto PreviewKind { get; set; }
    public bool CanEditWhileRunning { get; set; }
    public IList<PortDto> InputPorts { get; set; } = [];
    public IList<PortDto> OutputPorts { get; set; } = [];
    public IList<NodePropertyDefinitionDto> Properties { get; set; } = [];
}

public sealed class NodePropertyDefinitionDto
{
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public NodePropertyValueKindDto ValueKind { get; set; }
    public string? DefaultValue { get; set; }
    public bool IsReadOnly { get; set; }
    public double? Minimum { get; set; }
    public double? Maximum { get; set; }
    public double? Step { get; set; }
    public IList<PropertyOptionDto> Options { get; set; } = [];
}

public sealed class PropertyOptionDto
{
    public string Value { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
}
