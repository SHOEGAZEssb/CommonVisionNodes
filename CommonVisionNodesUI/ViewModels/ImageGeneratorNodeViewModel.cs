using CommonVisionNodes.Contracts;

namespace CommonVisionNodesUI.ViewModels;

public partial class ImageGeneratorNodeViewModel : NodeViewModel
{
    private readonly IReadOnlyList<string> _availablePatterns;

    public ImageGeneratorNodeViewModel(NodeDto node, NodeDefinitionDto definition)
        : base(node, definition)
    {
        _availablePatterns = [.. GetOptions("Pattern").Select(option => option.Value)];
		ImageWidth = GetInt("Width", 640);
		ImageHeight = GetInt("Height", 480);
        _pattern = GetString("Pattern", _availablePatterns.FirstOrDefault() ?? string.Empty);
		Speed = GetInt("Speed", 2);
    }

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

	public override string? Summary => $"{ImageWidth}x{ImageHeight}  {Pattern}";

    public override bool IsEditableWhileRunning => true;

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

    public override void ApplyImagePreview(ImagePreviewDto? preview)
    {
        PreviewImage = preview;
    }
}
