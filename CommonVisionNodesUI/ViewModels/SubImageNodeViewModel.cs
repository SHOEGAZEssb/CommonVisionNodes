using CommonVisionNodes.Contracts;

namespace CommonVisionNodesUI.ViewModels;

public partial class SubImageNodeViewModel : NodeViewModel
{
    public SubImageNodeViewModel(NodeDto node, NodeDefinitionDto definition)
        : base(node, definition)
    {
        _areaX = GetInt("AreaX", 0);
        _areaY = GetInt("AreaY", 0);
        _areaWidth = GetInt("AreaWidth", 64);
        _areaHeight = GetInt("AreaHeight", 64);
    }

    [ObservableProperty]
    private int _areaX;

    [ObservableProperty]
    private int _areaY;

    [ObservableProperty]
    private int _areaWidth;

    [ObservableProperty]
    private int _areaHeight;

    [ObservableProperty]
    private ImagePreviewDto? _previewImage;

    public override string? Summary => $"({AreaX}, {AreaY}) {AreaWidth}x{AreaHeight}";

    partial void OnAreaXChanged(int value)
    {
        SetInt("AreaX", value);
        RaiseSummaryChanged();
    }

    partial void OnAreaYChanged(int value)
    {
        SetInt("AreaY", value);
        RaiseSummaryChanged();
    }

    partial void OnAreaWidthChanged(int value)
    {
        SetInt("AreaWidth", value);
        RaiseSummaryChanged();
    }

    partial void OnAreaHeightChanged(int value)
    {
        SetInt("AreaHeight", value);
        RaiseSummaryChanged();
    }

    public override void ApplyImagePreview(ImagePreviewDto? preview)
    {
        PreviewImage = preview;
    }
}
