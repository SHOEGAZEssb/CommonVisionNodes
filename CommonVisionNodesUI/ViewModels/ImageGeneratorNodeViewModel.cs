using CommonVisionNodes.Contracts;

namespace CommonVisionNodesUI.ViewModels;

public partial class ImageGeneratorNodeViewModel : NodeViewModel
{
    public ImageGeneratorNodeViewModel(NodeDto node, NodeDefinitionDto definition)
        : base(node, definition)
    {
        _width = GetInt("Width", 640);
        _height = GetInt("Height", 480);
        _pattern = GetString("Pattern", GetOptions("Pattern").FirstOrDefault()?.Value ?? string.Empty);
        _speed = GetInt("Speed", 2);
    }

    public IReadOnlyList<string> AvailablePatterns => GetOptions("Pattern").Select(option => option.Value).ToList();

    [ObservableProperty]
    private int _width;

    [ObservableProperty]
    private int _height;

    [ObservableProperty]
    private string _pattern = string.Empty;

    [ObservableProperty]
    private int _speed;

    [ObservableProperty]
    private ImagePreviewDto? _previewImage;

    public override string? Summary => $"{Width}x{Height}  {Pattern}";

    public override bool IsEditableWhileRunning => true;

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

    partial void OnPatternChanged(string value)
    {
        SetString("Pattern", value);
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
