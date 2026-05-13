using System.Globalization;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using CommonVisionNodes.Contracts;
using Stemmer.Cvb;
using Stemmer.Cvb.Driver;

namespace CommonVisionNodes.Runtime;

public sealed class RuntimeNodeCatalog
{
    public IReadOnlyList<NodeDefinitionDto> GetDefinitions()
        => [.. CreateDefinitions()
            .OrderBy(definition => definition.Category, StringComparer.OrdinalIgnoreCase)
            .ThenBy(definition => definition.DisplayName, StringComparer.OrdinalIgnoreCase)];

    public NodeDefinitionDto? GetDefinition(string type)
        => CreateDefinitions().FirstOrDefault(definition => string.Equals(definition.Type, type, StringComparison.OrdinalIgnoreCase));

    public bool TryCreateNode(string type, out Node node)
    {
        node = type switch
        {
            nameof(ImageNode) => new ImageNode(),
            nameof(SaveImageNode) => new SaveImageNode(),
            nameof(DeviceNode) => new DeviceNode(),
            nameof(BinarizeNode) => new BinarizeNode(),
            nameof(SubImageNode) => new SubImageNode(),
            nameof(MatrixTransformNode) => new MatrixTransformNode(),
            nameof(ImageGeneratorNode) => new ImageGeneratorNode(),
            nameof(TimeTriggerNode) => new TimeTriggerNode(),
            nameof(ManualTriggerNode) => new ManualTriggerNode(),
            nameof(GevServerNode) => new GevServerNode(),
            nameof(FilterNode) => new FilterNode(),
            nameof(HistogramNode) => new HistogramNode(),
            nameof(MorphologyNode) => new MorphologyNode(),
            nameof(BlobNode) => new BlobNode(),
            nameof(NormalizeNode) => new NormalizeNode(),
            nameof(PolimagoClassifyNode) => new PolimagoClassifyNode(),
            nameof(GenericVisualizerNode) => new GenericVisualizerNode(),
            nameof(CSharpNode) => new CSharpNode(),
            _ => null!
        };

        return node is not null;
    }

    private IReadOnlyList<NodeDefinitionDto> CreateDefinitions()
    {
        return
        [
            CreateDefinition(
                nameof(ImageNode),
                "Image",
                "Input",
                "Load an image from disk.",
                "&#xE710;",
                NodePreviewKindDto.Image,
                canEditWhileRunning: false,
                () => new ImageNode(),
                StringProperty("FilePath", "File Path", "Path to the source image."),
                PreviewToggleProperty()),
            CreateDefinition(
                nameof(DeviceNode),
                "Device",
                "Input",
                "Acquire images from a local CVB device.",
                "&#xE714;",
                NodePreviewKindDto.Image,
                canEditWhileRunning: false,
                () => new DeviceNode(),
                EnumLikeProperty("AccessToken", "Device", "Select a discovered device.", GetDeviceOptions()),
                PreviewToggleProperty()),
            CreateDefinition(
                nameof(ImageGeneratorNode),
                "Generator",
                "Input",
                "Generate animated test images.",
                "&#xE768;",
                NodePreviewKindDto.Image,
                canEditWhileRunning: true,
                () => new ImageGeneratorNode(),
                IntProperty("Width", "Width", "Generated image width.", 1, 4096, 1),
                IntProperty("Height", "Height", "Generated image height.", 1, 4096, 1),
                EnumProperty<TestPattern>("Pattern", "Pattern", "The generator pattern."),
                IntProperty("Speed", "Speed", "Animation speed.", 1, 50, 1),
                PreviewToggleProperty()),
            CreateDefinition(
                nameof(TimeTriggerNode),
                "Time Trigger",
                "Trigger",
                "Send trigger signals at a fixed interval.",
                "&#xE916;",
                NodePreviewKindDto.None,
                canEditWhileRunning: true,
                () => new TimeTriggerNode(),
                DoubleProperty("IntervalSeconds", "Interval", "Seconds between trigger signals.", 0.001, null, 0.1)),
            CreateDefinition(
                nameof(ManualTriggerNode),
                "Manual Trigger",
                "Trigger",
                "Send a trigger signal when requested by the user.",
                "&#xE7C9;",
                NodePreviewKindDto.None,
                canEditWhileRunning: true,
                () => new ManualTriggerNode()),
            CreateDefinition(
                nameof(SaveImageNode),
                "Save Image",
                "Output",
                "Persist the current image to disk.",
                "&#xE74E;",
                NodePreviewKindDto.Image,
                canEditWhileRunning: false,
                () => new SaveImageNode(),
                StringProperty("FilePath", "File Path", "Destination path for the saved image."),
                PreviewToggleProperty()),
            CreateDefinition(
                nameof(GevServerNode),
                "GEV Server",
                "Output",
                "Expose incoming images as a GigE Vision server stream.",
                "&#xE774;",
                NodePreviewKindDto.Image,
                canEditWhileRunning: false,
                () => new GevServerNode(),
                AdapterProperty("LocalAddress", "Adapter", "Local IPv4 adapter address to bind."),
                EnumProperty<Stemmer.Cvb.GevServer.DriverType>("DriverType", "Driver", "GigE Vision transfer driver."),
                IntProperty("ResendBuffersCount", "Resend Buffers", "Full-frame packet resend buffer count.", 0, 64, 1),
                PreviewToggleProperty()),
            CreateDefinition(
                nameof(GenericVisualizerNode),
                "Visualizer",
                "Output",
                "Inspect any runtime value coming through the graph.",
                "&#xE7B3;",
                NodePreviewKindDto.Text,
                canEditWhileRunning: false,
                () => new GenericVisualizerNode(),
                PreviewToggleProperty(defaultValue: true)),
            CreateDefinition(
                nameof(BinarizeNode),
                "Binarize",
                "Processing",
                "Apply a binary threshold.",
                "&#xE71C;",
                NodePreviewKindDto.Image,
                canEditWhileRunning: true,
                () => new BinarizeNode(),
                IntProperty("Threshold", "Threshold", "Threshold between black and white.", 0, 255, 1),
                PreviewToggleProperty()),
            CreateDefinition(
                nameof(SubImageNode),
                "SubImage",
                "Processing",
                "Crop a rectangular region from the input image.",
                "&#xE740;",
                NodePreviewKindDto.Image,
                canEditWhileRunning: false,
                () => new SubImageNode(),
                IntProperty("AreaX", "X", "Crop origin X.", 0, null, 1),
                IntProperty("AreaY", "Y", "Crop origin Y.", 0, null, 1),
                IntProperty("AreaWidth", "Width", "Crop width.", 1, null, 1),
                IntProperty("AreaHeight", "Height", "Crop height.", 1, null, 1),
                PreviewToggleProperty()),
            CreateDefinition(
                nameof(MatrixTransformNode),
                "Transform",
                "Processing",
                "Rotate, scale, and translate an image.",
                "&#xE809;",
                NodePreviewKindDto.Image,
                canEditWhileRunning: true,
                () => new MatrixTransformNode(),
                DoubleProperty("Angle", "Rotation", "Rotation in degrees.", -180, 180, 0.5),
                DoubleProperty("ScaleX", "Scale X", "Horizontal scale.", 0.01, null, 0.1),
                DoubleProperty("ScaleY", "Scale Y", "Vertical scale.", 0.01, null, 0.1),
                DoubleProperty("TranslateX", "Translate X", "Horizontal translation in pixels.", null, null, 1),
                DoubleProperty("TranslateY", "Translate Y", "Vertical translation in pixels.", null, null, 1),
                PreviewToggleProperty()),
            CreateDefinition(
                nameof(FilterNode),
                "Filter",
                "Processing",
                "Apply a configurable CVB filter.",
                "&#xE71C;",
                NodePreviewKindDto.Image,
                canEditWhileRunning: true,
                () => new FilterNode(),
                EnumProperty<FilterType>("FilterType", "Filter", "The filter algorithm."),
                EnumProperty<KernelSize>("KernelSize", "Kernel Size", "Kernel size where applicable."),
                PreviewToggleProperty()),
            CreateDefinition(
                nameof(MorphologyNode),
                "Morphology",
                "Processing",
                "Apply dilate, erode, open, or close.",
                "&#xE71C;",
                NodePreviewKindDto.Image,
                canEditWhileRunning: true,
                () => new MorphologyNode(),
                EnumProperty<MorphologyOperation>("Operation", "Operation", "Morphological operation."),
                EnumProperty<KernelSize>("KernelSize", "Kernel Size", "Structuring element size."),
                PreviewToggleProperty()),
            CreateDefinition(
                nameof(NormalizeNode),
                "Normalize",
                "Processing",
                "Stretch the image range into a new output interval.",
                "&#xE793;",
                NodePreviewKindDto.Image,
                canEditWhileRunning: true,
                () => new NormalizeNode(),
                IntProperty("OutputMin", "Output Min", "Minimum output value.", 0, 255, 1),
                IntProperty("OutputMax", "Output Max", "Maximum output value.", 0, 255, 1),
                PreviewToggleProperty()),
            CreateDefinition(
                nameof(CSharpNode),
                "C# Script",
                "Processing",
                "Run custom image-processing code on the backend.",
                "&#xE943;",
                NodePreviewKindDto.Image,
                canEditWhileRunning: false,
                () => new CSharpNode(),
                MultilineTextProperty("Code", "Code", "Custom C# method body."),
                PreviewToggleProperty()),
            CreateDefinition(
                nameof(HistogramNode),
                "Histogram",
                "Analysis",
                "Compute histogram statistics from the current image.",
                "&#xE9D9;",
                NodePreviewKindDto.Histogram,
                canEditWhileRunning: false,
                () => new HistogramNode()),
            CreateDefinition(
                nameof(BlobNode),
                "Blob",
                "Analysis",
                "Detect connected components in a binary image.",
                "&#xE8B1;",
                NodePreviewKindDto.Blob,
                canEditWhileRunning: true,
                () => new BlobNode(),
                IntProperty("ForegroundThreshold", "Foreground Threshold", "Threshold used for foreground pixels.", 0, 255, 1),
                IntProperty("MinArea", "Min Area", "Minimum blob size.", 1, null, 1),
                IntProperty("MaxArea", "Max Area", "Maximum blob size. 0 disables the limit.", 0, null, 1),
                IntProperty("MaxBlobCount", "Max Blob Count", "Maximum number of results. 0 disables the limit.", 0, null, 1),
                BoolProperty("InvertForeground", "Invert Foreground", "Treat dark pixels as foreground."),
                BoolProperty("Use8Connectivity", "8-Connectivity", "Use diagonal connectivity when labeling blobs."),
                PreviewToggleProperty()),
            CreateDefinition(
                nameof(PolimagoClassifyNode),
                "Polimago Classify",
                "Analysis",
                "Classify the image or blob centers with a Polimago model.",
                "&#xE8BA;",
                NodePreviewKindDto.Classification,
                canEditWhileRunning: true,
                () => new PolimagoClassifyNode(),
                StringProperty("ClassifierPath", "Classifier File", "Path to the .clf file."),
                DoubleProperty("MinQuality", "Min Quality", "Minimum confidence threshold.", 0, 1, 0.05),
                PreviewToggleProperty())
        ];
    }

    private static NodeDefinitionDto CreateDefinition(
        string type,
        string displayName,
        string category,
        string description,
        string? iconGlyph,
        NodePreviewKindDto previewKind,
        bool canEditWhileRunning,
        Func<Node> factory,
        params NodePropertyDefinitionDto[] properties)
    {
        var node = factory();
        return new NodeDefinitionDto
        {
            Type = type,
            DisplayName = displayName,
            Category = category,
            Description = description,
            IconGlyph = iconGlyph,
            PreviewKind = previewKind,
            CanEditWhileRunning = canEditWhileRunning,
            InputPorts = [.. node.Inputs.Select(MapPort)],
            OutputPorts = [.. node.Outputs.Select(MapPort)],
            Properties = [.. ApplyDefaultValues(node, properties)]
		};
    }

    private static IEnumerable<NodePropertyDefinitionDto> ApplyDefaultValues(Node node, IEnumerable<NodePropertyDefinitionDto> properties)
    {
        var propertyMap = node.GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(property => property.CanRead)
            .ToDictionary(property => property.Name, StringComparer.OrdinalIgnoreCase);

        foreach (var definition in properties)
        {
            if (definition.DefaultValue is null && propertyMap.TryGetValue(definition.Name, out var property))
                definition.DefaultValue = ToInvariantString(property.GetValue(node));

            yield return definition;
        }
    }

    private static string? ToInvariantString(object? value)
    {
        return value switch
        {
            null => null,
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString()
        };
    }
    private static PortDto MapPort(Port port)
        => new()
        {
            Name = port.Name,
            Type = FormatTypeName(port.Type),
            Direction = port.Direction == PortDirection.Input ? PortDirectionDto.Input : PortDirectionDto.Output,
            Description = port.Description
        };

    private static string FormatTypeName(Type type)
    {
        if (type == typeof(Image))
            return "Image";

        if (type == typeof(TriggerSignal))
            return "Trigger";

        if (type == typeof(object))
            return "Any";

        if (type == typeof(string))
            return "String";

        if (type == typeof(int))
            return "Integer";

        if (!type.IsGenericType)
            return type.Name;

        var baseName = type.Name[..type.Name.IndexOf('`')];
        var args = string.Join(", ", type.GetGenericArguments().Select(FormatTypeName));
        return $"{baseName}<{args}>";
    }

    private static NodePropertyDefinitionDto StringProperty(string name, string displayName, string description)
        => new()
        {
            Name = name,
            DisplayName = displayName,
            Description = description,
            ValueKind = NodePropertyValueKindDto.String
        };

    private static NodePropertyDefinitionDto MultilineTextProperty(string name, string displayName, string description)
        => new()
        {
            Name = name,
            DisplayName = displayName,
            Description = description,
            ValueKind = NodePropertyValueKindDto.MultilineText
        };

    private static NodePropertyDefinitionDto BoolProperty(string name, string displayName, string description)
        => new()
        {
            Name = name,
            DisplayName = displayName,
            Description = description,
            ValueKind = NodePropertyValueKindDto.Boolean
        };

    private static NodePropertyDefinitionDto AdapterProperty(string name, string displayName, string description)
    {
        var options = GetAdapterOptions();
        return new()
        {
            Name = name,
            DisplayName = displayName,
            Description = description,
            ValueKind = NodePropertyValueKindDto.Enum,
            DefaultValue = options.FirstOrDefault()?.Value ?? IPAddress.Loopback.ToString(),
            Options = options
        };
    }

    private static NodePropertyDefinitionDto PreviewToggleProperty(bool defaultValue = false)
        => new()
        {
            Name = NodePreviewSettings.ShowPreviewPropertyName,
            DisplayName = "Show Preview",
            Description = "Render a live preview for this node.",
            ValueKind = NodePropertyValueKindDto.Boolean,
            DefaultValue = defaultValue.ToString()
        };

    private static NodePropertyDefinitionDto IntProperty(string name, string displayName, string description, double? min, double? max, double? step)
        => new()
        {
            Name = name,
            DisplayName = displayName,
            Description = description,
            ValueKind = NodePropertyValueKindDto.Integer,
            Minimum = min,
            Maximum = max,
            Step = step
        };

    private static NodePropertyDefinitionDto DoubleProperty(string name, string displayName, string description, double? min, double? max, double? step)
        => new()
        {
            Name = name,
            DisplayName = displayName,
            Description = description,
            ValueKind = NodePropertyValueKindDto.Double,
            Minimum = min,
            Maximum = max,
            Step = step
        };

    private static NodePropertyDefinitionDto EnumProperty<TEnum>(string name, string displayName, string description)
        where TEnum : struct, Enum
        => new()
        {
            Name = name,
            DisplayName = displayName,
            Description = description,
            ValueKind = NodePropertyValueKindDto.Enum,
            Options = [.. Enum.GetValues<TEnum>()
                .Select(value => new PropertyOptionDto
                {
                    Value = value.ToString(),
                    Label = SplitCamelCase(value.ToString())
                })]
		};

    private static NodePropertyDefinitionDto EnumLikeProperty(string name, string displayName, string description, IList<PropertyOptionDto> options)
        => new()
        {
            Name = name,
            DisplayName = displayName,
            Description = description,
            ValueKind = NodePropertyValueKindDto.Enum,
            Options = options
        };

    private static IList<PropertyOptionDto> GetAdapterOptions()
    {
        try
        {
            var adapters = new List<(bool IsLoopback, string Label, string Value)>();
            foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
            {
                try
                {
                    var isLoopbackInterface = networkInterface.NetworkInterfaceType == NetworkInterfaceType.Loopback;
                    if (!isLoopbackInterface && networkInterface.OperationalStatus != OperationalStatus.Up)
                        continue;

                    foreach (var unicastAddress in networkInterface.GetIPProperties().UnicastAddresses)
                    {
                        var address = unicastAddress.Address;
                        if (address.AddressFamily != AddressFamily.InterNetwork)
                            continue;

                        if (IPAddress.Any.Equals(address) || IPAddress.Broadcast.Equals(address))
                            continue;

                        var isLoopback = isLoopbackInterface || IPAddress.IsLoopback(address);
                        adapters.Add((isLoopback, $"{networkInterface.Name} ({address})", address.ToString()));
                    }
                }
                catch
                {
                    // Ignore adapters that cannot be inspected.
                }
            }

            var options = adapters
                .GroupBy(adapter => adapter.Value, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .OrderBy(adapter => adapter.IsLoopback)
                .ThenBy(adapter => adapter.Label, StringComparer.OrdinalIgnoreCase)
                .Select(adapter => new PropertyOptionDto
                {
                    Value = adapter.Value,
                    Label = adapter.Label
                })
                .ToList();

            return options.Count > 0
                ? options
                : [LoopbackAdapterOption()];
        }
        catch
        {
            return [LoopbackAdapterOption()];
        }
    }

    private static PropertyOptionDto LoopbackAdapterOption()
        => new()
        {
            Value = IPAddress.Loopback.ToString(),
            Label = $"Loopback ({IPAddress.Loopback})"
        };

    private static IList<PropertyOptionDto> GetDeviceOptions()
    {
        try
        {
            return [.. DeviceFactory.Discover(DiscoverFlags.IgnoreVins | DiscoverFlags.IncludeMockTL)
                .Select(info =>
                {
                    var label = info.TryGetProperty(DiscoveryProperties.DeviceModel, out var model)
                        ? model
                        : info.AccessToken;

                    if (info.TryGetProperty(DiscoveryProperties.DeviceSerialNumber, out var serial) && !string.IsNullOrWhiteSpace(serial))
                        label = $"{label} ({serial})";

                    return new PropertyOptionDto
                    {
                        Value = info.AccessToken,
                        Label = label
                    };
                })];
        }
        catch
        {
            return [];
        }
    }

    private static string SplitCamelCase(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return value;

        var builder = new StringBuilder(value.Length + 8);
        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            if (index > 0 && char.IsUpper(character) && !char.IsUpper(value[index - 1]))
                builder.Append(' ');

            builder.Append(character);
        }

        return builder.ToString();
    }
}

