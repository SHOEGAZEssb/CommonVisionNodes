using System.Globalization;
using CommonVisionNodes.Contracts;
using Windows.UI;

namespace CommonVisionNodesUI.ViewModels;

public abstract partial class NodeViewModel : ObservableObject
{
    private readonly Dictionary<string, NodePropertyDefinitionDto> _propertyDefinitions;

    public const double NodeWidth = 200;
    public const double HeaderHeight = 36;
    public const double PortHeight = 28;

    protected NodeViewModel(NodeDto node, NodeDefinitionDto definition)
    {
        Node = node;
        Definition = definition;
        _propertyDefinitions = definition.Properties.ToDictionary(property => property.Name, StringComparer.OrdinalIgnoreCase);
        EnsureDefaultProperties();

        _x = node.X;
        _y = node.Y;

        InputPorts = definition.InputPorts.Select((port, index) => new PortViewModel(port, this, index)).ToList();
        OutputPorts = definition.OutputPorts.Select((port, index) => new PortViewModel(port, this, index)).ToList();
    }

    public NodeDto Node { get; }

    public NodeDefinitionDto Definition { get; private set; }

    public string Title => Definition.DisplayName;

    public List<PortViewModel> InputPorts { get; }

    public List<PortViewModel> OutputPorts { get; }

    public virtual string? Summary => null;

    public virtual bool IsEditableWhileRunning => Definition.CanEditWhileRunning;

    [ObservableProperty]
    private string _executionTime = string.Empty;

    [ObservableProperty]
    private double _x;

    [ObservableProperty]
    private double _y;

    [ObservableProperty]
    private bool _isSelected;

    public double Height => HeaderHeight + Math.Max(InputPorts.Count, OutputPorts.Count) * PortHeight + 8;

    public Color HeaderColor => Definition.Type switch
    {
        "ImageNode" => Color.FromArgb(255, 74, 144, 217),
        "SaveImageNode" => Color.FromArgb(255, 102, 187, 106),
        "DeviceNode" => Color.FromArgb(255, 171, 71, 188),
        "BinarizeNode" => Color.FromArgb(255, 255, 152, 0),
        "SubImageNode" => Color.FromArgb(255, 0, 172, 193),
        "MatrixTransformNode" => Color.FromArgb(255, 233, 30, 99),
        "ImageGeneratorNode" => Color.FromArgb(255, 76, 175, 80),
        "FilterNode" => Color.FromArgb(255, 92, 107, 192),
        "HistogramNode" => Color.FromArgb(255, 239, 108, 0),
        "MorphologyNode" => Color.FromArgb(255, 121, 85, 72),
        "BlobNode" => Color.FromArgb(255, 0, 150, 136),
        "NormalizeNode" => Color.FromArgb(255, 255, 183, 77),
        "PolimagoClassifyNode" => Color.FromArgb(255, 123, 31, 162),
        "GenericVisualizerNode" => Color.FromArgb(255, 84, 110, 122),
        "CSharpNode" => Color.FromArgb(255, 90, 90, 90),
        _ => Color.FromArgb(255, 128, 128, 128)
    };

    public NodeDto ToNodeDtoClone()
        => new()
        {
            Id = Node.Id,
            Type = Node.Type,
            X = X,
            Y = Y,
            Properties = Node.Properties.Select(property => new NodePropertyDto
            {
                Name = property.Name,
                Value = property.Value
            }).ToList()
        };

    public virtual void RefreshDefinition(NodeDefinitionDto definition)
    {
        Definition = definition;
        _propertyDefinitions.Clear();
        foreach (var property in definition.Properties)
            _propertyDefinitions[property.Name] = property;

        EnsureDefaultProperties();
        OnDefinitionUpdated();
        OnPropertyChanged(nameof(IsEditableWhileRunning));
        OnPropertyChanged(nameof(Summary));
    }

    public void ApplyExecutionUpdate(NodeExecutionUpdateDto update)
    {
        if (update.ExecutionDurationMs.HasValue)
            ExecutionTime = FormatExecutionTime(update.ExecutionDurationMs.Value);

        OnExecutionUpdate(update);
    }

    public virtual void ApplyImagePreview(ImagePreviewDto? preview) { }

    public virtual void ApplyHistogramPreview(HistogramPreviewDto preview) { }

    public virtual void ApplyBlobPreview(BlobPreviewDto preview) { }

    public virtual void ApplyClassificationPreview(ClassificationPreviewDto preview) { }

    public virtual void ApplyTextPreview(TextPreviewDto preview) { }

    protected virtual void OnExecutionUpdate(NodeExecutionUpdateDto update) { }

    protected virtual void OnDefinitionUpdated() { }

    protected string GetString(string name, string defaultValue = "")
        => GetProperty(name)?.Value ?? defaultValue;

    protected int GetInt(string name, int defaultValue = 0)
        => int.TryParse(GetProperty(name)?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : defaultValue;

    protected double GetDouble(string name, double defaultValue = 0)
        => double.TryParse(GetProperty(name)?.Value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var value)
            ? value
            : defaultValue;

    protected bool GetBool(string name, bool defaultValue = false)
        => bool.TryParse(GetProperty(name)?.Value, out var value)
            ? value
            : defaultValue;

    protected IReadOnlyList<PropertyOptionDto> GetOptions(string name)
        => _propertyDefinitions.TryGetValue(name, out var definition)
            ? definition.Options.ToList()
            : [];

    protected void SetString(string name, string? value)
    {
        EnsureProperty(name).Value = value;
        OnPropertyChanged(nameof(Summary));
    }

    protected void SetInt(string name, int value)
        => SetString(name, value.ToString(CultureInfo.InvariantCulture));

    protected void SetDouble(string name, double value)
        => SetString(name, value.ToString(CultureInfo.InvariantCulture));

    protected void SetBool(string name, bool value)
        => SetString(name, value.ToString());

    protected void RaiseSummaryChanged() => OnPropertyChanged(nameof(Summary));

    private void EnsureDefaultProperties()
    {
        foreach (var property in Definition.Properties)
            EnsureProperty(property.Name, property.DefaultValue);
    }

    private NodePropertyDto? GetProperty(string name)
        => Node.Properties.FirstOrDefault(property => string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase));

    private NodePropertyDto EnsureProperty(string name, string? defaultValue = null)
    {
        var property = GetProperty(name);
        if (property is not null)
            return property;

        property = new NodePropertyDto
        {
            Name = name,
            Value = defaultValue
        };
        Node.Properties.Add(property);
        return property;
    }

    partial void OnXChanged(double value)
    {
        Node.X = value;
        NotifyPortPositions();
    }

    partial void OnYChanged(double value)
    {
        Node.Y = value;
        NotifyPortPositions();
    }

    private void NotifyPortPositions()
    {
        foreach (var port in InputPorts)
            port.NotifyPositionChanged();

        foreach (var port in OutputPorts)
            port.NotifyPositionChanged();
    }

    private static string FormatExecutionTime(double executionDurationMs)
        => executionDurationMs >= 1.0
            ? $"{executionDurationMs:F1} ms"
            : $"{executionDurationMs * 1000:F0} us";
}

