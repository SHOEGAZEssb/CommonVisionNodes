using System.Globalization;
using System.Reflection;
using CommonVisionNodes.Contracts;
using Windows.UI;

namespace CommonVisionNodesUI.ViewModels;

/// <summary>
/// Base view model for a graph node shown on the editor canvas.
/// </summary>
public abstract partial class NodeViewModel : ObservableObject
{
	private readonly Dictionary<string, NodePropertyDefinitionDto> _propertyDefinitions;

	/// <summary>
	/// Default node width in canvas units.
	/// </summary>
	public const double NodeWidth = 200;

	/// <summary>
	/// Default preview area height in canvas units.
	/// </summary>
	public const double DefaultPreviewHeight = 124;

	/// <summary>
	/// Minimum node width in canvas units.
	/// </summary>
	public const double MinNodeWidth = 160;

	/// <summary>
	/// Minimum node height in canvas units.
	/// </summary>
	public const double MinNodeHeight = 104;

	/// <summary>
	/// Fixed node header height in canvas units.
	/// </summary>
	public const double HeaderHeight = 36;

	/// <summary>
	/// Fixed vertical spacing allocated to each port row.
	/// </summary>
	public const double PortHeight = 28;

	/// <summary>
	/// Creates a node view model from serialized node state and catalog metadata.
	/// </summary>
	/// <param name="node">Serialized node instance.</param>
	/// <param name="definition">Catalog definition for the node type.</param>
	protected NodeViewModel(NodeDto node, NodeDefinitionDto definition)
	{
		Node = node;
		Definition = definition;
		_propertyDefinitions = definition.Properties.ToDictionary(property => property.Name, StringComparer.OrdinalIgnoreCase);
		EnsureDefaultProperties();
		ShowPreview = ReadShowPreview();

		InputPorts = [.. definition.InputPorts.Select((port, index) => new PortViewModel(port, this, index))];
		OutputPorts = [.. definition.OutputPorts.Select((port, index) => new PortViewModel(port, this, index))];

		X = node.X;
		Y = node.Y;
		Width = NormalizeDimension(node.Width, NodeWidth, MinNodeWidth);
		Height = NormalizeDimension(node.Height, GetDefaultHeight(), MinimumContentHeight);
	}

	/// <summary>
	/// Serialized node instance backing this view model.
	/// </summary>
	public NodeDto Node { get; }

	/// <summary>
	/// Catalog definition currently describing this node type.
	/// </summary>
	public NodeDefinitionDto Definition { get; private set; }

	/// <summary>
	/// Raised when serialized node configuration changes.
	/// </summary>
	public event EventHandler? ConfigurationChanged;

	/// <summary>
	/// User-facing node title.
	/// </summary>
	public string Title => Definition.DisplayName;

	/// <summary>
	/// View models for input ports.
	/// </summary>
	public List<PortViewModel> InputPorts { get; }

	/// <summary>
	/// View models for output ports.
	/// </summary>
	public List<PortViewModel> OutputPorts { get; }

	/// <summary>
	/// Short status/configuration text displayed inside the node.
	/// </summary>
	public virtual string? Summary => null;

	/// <summary>
	/// Indicates whether the property panel should remain editable during continuous execution.
	/// </summary>
	public virtual bool IsEditableWhileRunning => Definition.CanEditWhileRunning;

	/// <summary>
	/// Indicates whether this node exposes the synthetic preview toggle property.
	/// </summary>
	public bool SupportsPreviewToggle => _propertyDefinitions.ContainsKey(NodePreviewSettings.ShowPreviewPropertyName);

	/// <summary>
	/// Indicates whether this node has preview content whose viewport can be resized.
	/// </summary>
	public bool CanResize => Definition.PreviewKind != NodePreviewKindDto.None;

	[ObservableProperty]
	public partial string ExecutionTime { get; set; } = string.Empty;

	[ObservableProperty]
	public partial double X { get; set; }

	[ObservableProperty]
	public partial double Y { get; set; }

	[ObservableProperty]
	public partial double Width { get; set; }

	[ObservableProperty]
	public partial double Height { get; set; }

	[ObservableProperty]
	public partial bool IsSelected { get; set; }

	[ObservableProperty]
	public partial bool ShowPreview { get; set; }

	[ObservableProperty]
	public partial bool IsGraphRunning { get; set; }

	[ObservableProperty]
	public partial NodeExecutionStatusDto ExecutionStatus { get; set; } = NodeExecutionStatusDto.Pending;

	[ObservableProperty]
	public partial string ExecutionMessage { get; set; } = string.Empty;

	/// <summary>
	/// Indicates whether the most recent runtime update for this node reported a failure.
	/// </summary>
	public bool HasExecutionError => ExecutionStatus == NodeExecutionStatusDto.Failed;

	/// <summary>
	/// Error text displayed near the node when execution fails.
	/// </summary>
	public string ExecutionErrorText => HasExecutionError
		? string.IsNullOrWhiteSpace(ExecutionMessage)
			? "Node execution failed."
			: ExecutionMessage
		: string.Empty;

	/// <summary>
	/// Smallest height that keeps the header and ports visible.
	/// </summary>
	public double MinimumContentHeight => Math.Max(MinNodeHeight, HeaderHeight + Math.Max(InputPorts.Count, OutputPorts.Count) * PortHeight + 24);

	/// <summary>
	/// Explains why this node's main property editor is disabled while execution is running.
	/// </summary>
	public virtual string RuntimeEditLockMessage => "Stop execution to edit these properties.";

	/// <summary>
	/// Header color used to visually group node types on the canvas.
	/// </summary>
	public Color HeaderColor => Definition.Type switch
	{
		"ImageNode" => Color.FromArgb(255, 74, 144, 217),
		"SaveImageNode" => Color.FromArgb(255, 102, 187, 106),
		"GevServerNode" => Color.FromArgb(255, 38, 166, 154),
		"DeviceNode" => Color.FromArgb(255, 171, 71, 188),
		"BinarizeNode" => Color.FromArgb(255, 255, 152, 0),
		"SubImageNode" => Color.FromArgb(255, 0, 172, 193),
		"MatrixTransformNode" => Color.FromArgb(255, 233, 30, 99),
		"ImageGeneratorNode" => Color.FromArgb(255, 76, 175, 80),
		"TimeTriggerNode" => Color.FromArgb(255, 66, 165, 245),
		"ManualTriggerNode" => Color.FromArgb(255, 126, 87, 194),
		"FilterNode" => Color.FromArgb(255, 92, 107, 192),
		"HistogramNode" => Color.FromArgb(255, 239, 108, 0),
		"MorphologyNode" => Color.FromArgb(255, 121, 85, 72),
		"BlobNode" => Color.FromArgb(255, 0, 150, 136),
		"NormalizeNode" => Color.FromArgb(255, 255, 183, 77),
		"MinosSearchNode" => Color.FromArgb(255, 94, 53, 177),
		"PolimagoClassifyNode" => Color.FromArgb(255, 123, 31, 162),
		"CodeReaderNode" => Color.FromArgb(255, 0, 121, 107),
		"GenericVisualizerNode" => Color.FromArgb(255, 84, 110, 122),
		"CSharpNode" => Color.FromArgb(255, 90, 90, 90),
		_ => Color.FromArgb(255, 128, 128, 128)
	};

	/// <summary>
	/// Creates a detached DTO snapshot suitable for saving or sending to the backend.
	/// </summary>
	/// <returns>A clone of the serialized node state.</returns>
	public NodeDto ToNodeDtoClone()
		=> new()
		{
			Id = Node.Id,
			Type = Node.Type,
			X = X,
			Y = Y,
			Width = Width,
			Height = Height,
			Properties = [.. Node.Properties.Select(property => new NodePropertyDto
			{
				Name = property.Name,
				Value = property.Value
			})]
		};

	/// <summary>
	/// Applies updated catalog metadata while preserving node property values.
	/// </summary>
	/// <param name="definition">Updated node definition.</param>
	public virtual void RefreshDefinition(NodeDefinitionDto definition)
	{
		Definition = definition;
		_propertyDefinitions.Clear();
		foreach (var property in definition.Properties)
			_propertyDefinitions[property.Name] = property;

		EnsureDefaultProperties();
		SyncShowPreview();
		OnDefinitionUpdated();
		OnPropertyChanged(nameof(IsEditableWhileRunning));
		OnPropertyChanged(nameof(SupportsPreviewToggle));
		OnPropertyChanged(nameof(CanResize));
		OnPropertyChanged(nameof(Summary));
	}

	/// <summary>
	/// Applies a per-node execution update received from the backend.
	/// </summary>
	/// <param name="update">Execution update.</param>
	public void ApplyExecutionUpdate(NodeExecutionUpdateDto update)
	{
		if (update.ExecutionDurationMs.HasValue)
			ExecutionTime = FormatExecutionTime(update.ExecutionDurationMs.Value);

		ExecutionStatus = update.Status;
		ExecutionMessage = update.Message ?? string.Empty;
		OnExecutionUpdate(update);
	}

	/// <summary>
	/// Applies an overall execution state update.
	/// </summary>
	/// <param name="state">Execution state update.</param>
	public void ApplyExecutionState(ExecutionStateDto state)
	{
		if (state.Status is ExecutionStatusDto.Starting or ExecutionStatusDto.Initializing)
			ClearExecutionState();

		OnExecutionState(state);
	}

	/// <summary>
	/// Applies an image preview payload to node-specific state.
	/// </summary>
	/// <param name="preview">Image preview payload, or <c>null</c> to clear it.</param>
	public virtual void ApplyImagePreview(ImagePreviewDto? preview) { }

	/// <summary>
	/// Applies a histogram preview payload to node-specific state.
	/// </summary>
	/// <param name="preview">Histogram preview payload.</param>
	public virtual void ApplyHistogramPreview(HistogramPreviewDto preview) { }

	/// <summary>
	/// Applies a blob preview payload to node-specific state.
	/// </summary>
	/// <param name="preview">Blob preview payload.</param>
	public virtual void ApplyBlobPreview(BlobPreviewDto preview) { }

	/// <summary>
	/// Applies a classification preview payload to node-specific state.
	/// </summary>
	/// <param name="preview">Classification preview payload.</param>
	public virtual void ApplyClassificationPreview(ClassificationPreviewDto preview) { }

	/// <summary>
	/// Applies a CodeReader preview payload to node-specific state.
	/// </summary>
	/// <param name="preview">CodeReader preview payload.</param>
	public virtual void ApplyCodeReaderPreview(CodeReaderPreviewDto preview) { }

	/// <summary>
	/// Applies a text preview payload to node-specific state.
	/// </summary>
	/// <param name="preview">Text preview payload.</param>
	public virtual void ApplyTextPreview(TextPreviewDto preview) { }

	/// <summary>
	/// Allows derived node view models to react to node execution updates.
	/// </summary>
	/// <param name="update">Execution update.</param>
	protected virtual void OnExecutionUpdate(NodeExecutionUpdateDto update) { }

	/// <summary>
	/// Allows derived node view models to react to overall execution state changes.
	/// </summary>
	/// <param name="state">Execution state update.</param>
	protected virtual void OnExecutionState(ExecutionStateDto state) { }

	/// <summary>
	/// Allows derived node view models to resynchronize option lists after catalog updates.
	/// </summary>
	protected virtual void OnDefinitionUpdated() { }

	/// <summary>
	/// Reads a string property from the backing node.
	/// </summary>
	/// <param name="name">Property name.</param>
	/// <param name="defaultValue">Fallback value.</param>
	/// <returns>The stored value or fallback.</returns>
	protected string GetString(string name, string defaultValue = "")
		=> GetProperty(name)?.Value ?? defaultValue;

	/// <summary>
	/// Reads an invariant-culture integer property from the backing node.
	/// </summary>
	/// <param name="name">Property name.</param>
	/// <param name="defaultValue">Fallback value.</param>
	/// <returns>The parsed value or fallback.</returns>
	protected int GetInt(string name, int defaultValue = 0)
		=> int.TryParse(GetProperty(name)?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
			? value
			: defaultValue;

	/// <summary>
	/// Reads an invariant-culture floating-point property from the backing node.
	/// </summary>
	/// <param name="name">Property name.</param>
	/// <param name="defaultValue">Fallback value.</param>
	/// <returns>The parsed value or fallback.</returns>
	protected double GetDouble(string name, double defaultValue = 0)
		=> double.TryParse(GetProperty(name)?.Value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var value)
			? value
			: defaultValue;

	/// <summary>
	/// Reads a Boolean property from the backing node.
	/// </summary>
	/// <param name="name">Property name.</param>
	/// <param name="defaultValue">Fallback value.</param>
	/// <returns>The parsed value or fallback.</returns>
	protected bool GetBool(string name, bool defaultValue = false)
		=> bool.TryParse(GetProperty(name)?.Value, out var value)
			? value
			: defaultValue;

	/// <summary>
	/// Gets catalog options for an enum-like property.
	/// </summary>
	/// <param name="name">Property name.</param>
	/// <returns>Available options, or an empty list.</returns>
	protected IReadOnlyList<PropertyOptionDto> GetOptions(string name)
		=> _propertyDefinitions.TryGetValue(name, out var definition)
			? definition.Options.ToList()
			: [];

	/// <summary>
	/// Returns the first available value for an enum-like property, or a fallback when it has no options.
	/// </summary>
	protected static string FirstOptionOrDefault(IReadOnlyList<string> options, string fallback = "")
		=> options.Count > 0 ? options[0] : fallback;

	/// <summary>
	/// Updates an enum-like property while ignoring transient empty selections from unloading editors.
	/// </summary>
	protected bool SetOptionValue(ref string field, string? value, string propertyName)
	{
		var nextValue = value ?? string.Empty;
		if (string.IsNullOrWhiteSpace(nextValue) && !string.IsNullOrWhiteSpace(field))
			return false;

		if (!SetProperty(ref field, nextValue, propertyName))
			return false;

		SetString(propertyName, nextValue);
		return true;
	}

	/// <summary>
	/// Stores a string property value and raises configuration change notifications when requested.
	/// </summary>
	/// <param name="name">Property name.</param>
	/// <param name="value">Serialized value.</param>
	/// <param name="notifyConfigurationChanged">Whether the stored value changes runtime node configuration.</param>
	protected void SetString(string name, string? value, bool notifyConfigurationChanged = true)
	{
		EnsureProperty(name).Value = value;
		OnPropertyChanged(nameof(Summary));
		if (notifyConfigurationChanged)
			ConfigurationChanged?.Invoke(this, EventArgs.Empty);
	}

	/// <summary>
	/// Stores an integer property value using invariant-culture formatting.
	/// </summary>
	/// <param name="name">Property name.</param>
	/// <param name="value">Property value.</param>
	protected void SetInt(string name, int value)
		=> SetString(name, value.ToString(CultureInfo.InvariantCulture));

	/// <summary>
	/// Stores a floating-point property value using invariant-culture formatting.
	/// </summary>
	/// <param name="name">Property name.</param>
	/// <param name="value">Property value.</param>
	protected void SetDouble(string name, double value)
		=> SetString(name, value.ToString(CultureInfo.InvariantCulture));

	/// <summary>
	/// Stores a Boolean property value.
	/// </summary>
	/// <param name="name">Property name.</param>
	/// <param name="value">Property value.</param>
	protected void SetBool(string name, bool value, bool notifyConfigurationChanged = true)
		=> SetString(name, value.ToString(), notifyConfigurationChanged);

	/// <summary>
	/// Raises a property notification for <see cref="Summary"/>.
	/// </summary>
	protected void RaiseSummaryChanged() => OnPropertyChanged(nameof(Summary));

	partial void OnShowPreviewChanged(bool value)
	{
		if (!SupportsPreviewToggle)
			return;

		SetBool(NodePreviewSettings.ShowPreviewPropertyName, value, notifyConfigurationChanged: false);
		if (!value)
			ClearPreviewState();
	}

	partial void OnIsGraphRunningChanged(bool value)
	{
		OnRuntimeEditStateChanged();
	}

	partial void OnExecutionStatusChanged(NodeExecutionStatusDto value)
	{
		OnPropertyChanged(nameof(HasExecutionError));
		OnPropertyChanged(nameof(ExecutionErrorText));
	}

	partial void OnExecutionMessageChanged(string value)
	{
		OnPropertyChanged(nameof(HasExecutionError));
		OnPropertyChanged(nameof(ExecutionErrorText));
	}

	/// <summary>
	/// Allows specialized node editors to refresh per-property runtime edit state.
	/// </summary>
	protected virtual void OnRuntimeEditStateChanged() { }

	private void EnsureDefaultProperties()
	{
		foreach (var property in Definition.Properties)
			EnsureProperty(property.Name, property.DefaultValue);
	}

	private bool ReadShowPreview()
		=> SupportsPreviewToggle
			&& GetBool(NodePreviewSettings.ShowPreviewPropertyName, NodePreviewSettings.IsEnabledByDefault(Definition.Type));

	private void SyncShowPreview()
	{
		var showPreview = ReadShowPreview();
		if (ShowPreview == showPreview)
			return;

		ShowPreview = showPreview;
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

	partial void OnWidthChanged(double value)
	{
		var normalized = NormalizeDimension(value, NodeWidth, MinNodeWidth);
		if (Math.Abs(normalized - value) > 0.01)
		{
			Width = normalized;
			return;
		}

		Node.Width = normalized;
		NotifyPortPositions();
	}

	partial void OnHeightChanged(double value)
	{
		var normalized = NormalizeDimension(value, GetDefaultHeight(), MinimumContentHeight);
		if (Math.Abs(normalized - value) > 0.01)
		{
			Height = normalized;
			return;
		}

		Node.Height = normalized;
	}

	private void NotifyPortPositions()
	{
		if (InputPorts is not null)
		{
			foreach (var port in InputPorts)
				port.NotifyPositionChanged();
		}

		if (OutputPorts is not null)
		{
			foreach (var port in OutputPorts)
				port.NotifyPositionChanged();
		}
	}

	private static string FormatExecutionTime(double executionDurationMs)
		=> executionDurationMs >= 1.0
			? $"{executionDurationMs:F1} ms"
			: $"{executionDurationMs * 1000:F0} us";

	private static double NormalizeDimension(double value, double fallback, double minimum)
		=> double.IsFinite(value) && value > 0
			? Math.Max(minimum, value)
			: Math.Max(minimum, fallback);

	private double GetDefaultHeight()
	{
		var hasDefaultPreviewArea = Definition.PreviewKind is NodePreviewKindDto.Histogram
			|| ShowPreview && Definition.PreviewKind is not NodePreviewKindDto.None;

		return hasDefaultPreviewArea
			? MinimumContentHeight + DefaultPreviewHeight
			: MinimumContentHeight;
	}

	private void ClearExecutionState()
	{
		if (ExecutionStatus != NodeExecutionStatusDto.Pending)
			ExecutionStatus = NodeExecutionStatusDto.Pending;

		if (!string.IsNullOrEmpty(ExecutionMessage))
			ExecutionMessage = string.Empty;
	}

	private void ClearPreviewState()
	{
		const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance;

		// Preview properties live only on specialized view models. Reflection keeps the base
		// class from needing an interface for every preview flavor while still clearing stale UI.
		var previewImageProperty = GetType().GetProperty("PreviewImage", flags);
		if (previewImageProperty?.CanWrite == true && previewImageProperty.PropertyType == typeof(ImagePreviewDto))
			previewImageProperty.SetValue(this, null);

		var displayTextProperty = GetType().GetProperty("DisplayText", flags);
		if (displayTextProperty?.CanWrite == true && displayTextProperty.PropertyType == typeof(string))
			displayTextProperty.SetValue(this, string.Empty);
	}
}
