using CommonVisionNodes;
using CvbImage = Stemmer.Cvb.Image;

namespace CommonVisionNodesUI.ViewModels;

/// <summary>
/// View model for <see cref="ImageGeneratorNode"/>. Manages pattern, size, speed, and preview.
/// </summary>
public partial class ImageGeneratorNodeViewModel : NodeViewModel
{
    private readonly ImageGeneratorNode _generatorNode;

    [ObservableProperty]
    private int _width = 640;

    [ObservableProperty]
    private int _height = 480;

    [ObservableProperty]
    private TestPattern _pattern = TestPattern.GradientH;

    [ObservableProperty]
    private int _speed = 2;

    [ObservableProperty]
    private CvbImage? _previewImage;

    /// <inheritdoc/>
    public override string? Summary => $"{Width}×{Height} {Pattern}";

    /// <inheritdoc/>
    public override bool IsEditableWhileRunning => true;

    /// <summary>
    /// Creates a new image generator node view model.
    /// </summary>
    /// <param name="node">The underlying image generator node.</param>
    /// <param name="x">Initial X position.</param>
    /// <param name="y">Initial Y position.</param>
    public ImageGeneratorNodeViewModel(ImageGeneratorNode node, double x, double y) : base(node, x, y)
    {
        _generatorNode = node;
        _width = node.Width;
        _height = node.Height;
        _pattern = node.Pattern;
        _speed = node.Speed;
    }

    /// <summary>
    /// Available test patterns for the combo box.
    /// </summary>
    public TestPattern[] AvailablePatterns { get; } = Enum.GetValues<TestPattern>();

    partial void OnWidthChanged(int value)
    {
        if (!IsSelected) { _width = _generatorNode.Width; return; }
        _generatorNode.Width = value;
        OnPropertyChanged(nameof(Summary));
    }

    partial void OnHeightChanged(int value)
    {
        if (!IsSelected) { _height = _generatorNode.Height; return; }
        _generatorNode.Height = value;
        OnPropertyChanged(nameof(Summary));
    }

    partial void OnPatternChanged(TestPattern value)
    {
        if (!IsSelected) { _pattern = _generatorNode.Pattern; return; }
        _generatorNode.Pattern = value;
        OnPropertyChanged(nameof(Summary));
    }

    partial void OnSpeedChanged(int value)
    {
        if (!IsSelected) { _speed = _generatorNode.Speed; return; }
        _generatorNode.Speed = value;
    }

    /// <summary>
    /// Updates the preview image from the generator output.
    /// </summary>
    public void RefreshPreview()
    {
        PreviewImage = _generatorNode.ImageOutput.Value as CvbImage;
    }
}
