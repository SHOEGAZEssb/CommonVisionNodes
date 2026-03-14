using CommonVisionNodes.Contracts;

namespace CommonVisionNodesUI.ViewModels;

public partial class ImageGeneratorNodeViewModel : NodeViewModel
{
    private readonly IReadOnlyList<string> _availablePatterns;

    public ImageGeneratorNodeViewModel(NodeDto node, NodeDefinitionDto definition)
        : base(node, definition)
    {
        _availablePatterns = GetOptions("Pattern").Select(option => option.Value).ToList();
        _width = GetInt("Width", 640);
        _height = GetInt("Height", 480);
        _pattern = GetString("Pattern", _availablePatterns.FirstOrDefault() ?? string.Empty);
        _speed = GetInt("Speed", 2);
    }

    public IReadOnlyList<string> AvailablePatterns => _availablePatterns;

    [ObservableProperty]
    private int _width;

    [ObservableProperty]
    private int _height;

    private string _pattern = string.Empty;

    [ObservableProperty]
    private int _speed;

    [ObservableProperty]
    private ImagePreviewDto? _previewImage;

    public override string? Summary => $"{Width}x{Height}  {Pattern}";

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

    partial void OnWidthChanged(int value)
    {
        SetInt("Width", value);
        RaiseSummaryChanged();
    }

    partial void OnHeightChanged(int value)
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
