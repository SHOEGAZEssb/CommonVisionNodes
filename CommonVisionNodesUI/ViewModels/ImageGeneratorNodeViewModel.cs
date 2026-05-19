using CommonVisionNodes.Contracts;

namespace CommonVisionNodesUI.ViewModels;

/// <summary>
/// View model for a synthetic image generator node.
/// </summary>
public partial class ImageGeneratorNodeViewModel : NodeViewModel
{
    private readonly IReadOnlyList<string> _availablePatterns;

    /// <summary>
    /// Creates an image generator node view model.
    /// </summary>
    /// <param name="node">Serialized node instance.</param>
    /// <param name="definition">Catalog definition.</param>
    public ImageGeneratorNodeViewModel(NodeDto node, NodeDefinitionDto definition)
        : base(node, definition)
    {
        _availablePatterns = [.. GetOptions("Pattern").Select(option => option.Value)];
		ImageWidth = GetInt("Width", 640);
		ImageHeight = GetInt("Height", 480);
        _pattern = GetString("Pattern", _availablePatterns.FirstOrDefault() ?? string.Empty);
		Speed = GetInt("Speed", 2);
    }

    /// <summary>
    /// Available test pattern names.
    /// </summary>
    public IReadOnlyList<string> AvailablePatterns => _availablePatterns;

	[ObservableProperty]
	public partial int ImageWidth { get; set; }

	[ObservableProperty]
	public partial int ImageHeight { get; set; }

	private string _pattern = string.Empty;

	[ObservableProperty]
	public partial int Speed { get; set; }

	[ObservableProperty]
	public partial ImagePreviewDto? PreviewImage { get; set; }

    /// <inheritdoc/>
    public override string? Summary => $"{ImageWidth}x{ImageHeight}  {Pattern}";

    /// <inheritdoc/>
    public override bool IsEditableWhileRunning => true;

    /// <summary>
    /// Selected test pattern name.
    /// </summary>
    public string Pattern
    {
        get => _pattern;
        set
        {
            var nextValue = value ?? string.Empty;
            if (string.IsNullOrWhiteSpace(nextValue) && !string.IsNullOrWhiteSpace(_pattern))
                return;

            if (SetProperty(ref _pattern, nextValue))
            {
                SetString("Pattern", nextValue);
                RaiseSummaryChanged();
            }
        }
    }

    partial void OnImageWidthChanged(int value)
    {
        SetInt("Width", value);
        RaiseSummaryChanged();
    }

    partial void OnImageHeightChanged(int value)
    {
        SetInt("Height", value);
        RaiseSummaryChanged();
    }

    partial void OnSpeedChanged(int value)
    {
        SetInt("Speed", value);
        RaiseSummaryChanged();
    }

    /// <inheritdoc/>
    public override void ApplyImagePreview(ImagePreviewDto? preview)
    {
        PreviewImage = preview;
    }
}
