using CommonVisionNodes.Contracts;

namespace CommonVisionNodesUI.ViewModels;

/// <summary>
/// View model for a CVB CodeReader node.
/// </summary>
public partial class CodeReaderNodeViewModel : NodeViewModel
{
    private readonly IReadOnlyList<string> _availableSymbologies;
    private readonly IReadOnlyList<string> _availablePolarities;
    private readonly IReadOnlyList<string> _availableSearchSpeeds;
    private readonly IReadOnlyList<string> _availablePerformanceModes;
    private string _symbologies = string.Empty;
    private string _codePolarity = string.Empty;
    private string _codeSearchSpeed = string.Empty;
    private string _performanceMode = string.Empty;

    /// <summary>
    /// Creates a CodeReader node view model.
    /// </summary>
    /// <param name="node">Serialized node instance.</param>
    /// <param name="definition">Catalog definition.</param>
    public CodeReaderNodeViewModel(NodeDto node, NodeDefinitionDto definition)
        : base(node, definition)
    {
        _availableSymbologies = [.. GetOptions("Symbologies").Select(option => option.Value)];
        _availablePolarities = [.. GetOptions("CodePolarity").Select(option => option.Value)];
        _availableSearchSpeeds = [.. GetOptions("CodeSearchSpeed").Select(option => option.Value)];
        _availablePerformanceModes = [.. GetOptions("PerformanceMode").Select(option => option.Value)];

        _symbologies = GetString("Symbologies", _availableSymbologies.FirstOrDefault() ?? string.Empty);
        _codePolarity = GetString("CodePolarity", _availablePolarities.FirstOrDefault() ?? string.Empty);
        _codeSearchSpeed = GetString("CodeSearchSpeed", _availableSearchSpeeds.FirstOrDefault() ?? string.Empty);
        _performanceMode = GetString("PerformanceMode", _availablePerformanceModes.FirstOrDefault() ?? string.Empty);

        DetectorDensity = GetInt("DetectorDensity", 3);
        MaxCodes = GetInt("MaxCodes", 0);
        TimeLimitMs = GetInt("TimeLimitMs", 0);
        BasicInkjetDpmEnabled = GetBool("BasicInkjetDpmEnabled", false);
    }

    /// <summary>
    /// Available symbology presets.
    /// </summary>
    public IReadOnlyList<string> AvailableSymbologies => _availableSymbologies;

    /// <summary>
    /// Available Data Matrix/QR polarity choices.
    /// </summary>
    public IReadOnlyList<string> AvailablePolarities => _availablePolarities;

    /// <summary>
    /// Available search speed choices.
    /// </summary>
    public IReadOnlyList<string> AvailableSearchSpeeds => _availableSearchSpeeds;

    /// <summary>
    /// Available performance mode choices.
    /// </summary>
    public IReadOnlyList<string> AvailablePerformanceModes => _availablePerformanceModes;

    /// <summary>
    /// Selected symbology preset.
    /// </summary>
    public string Symbologies
    {
        get => _symbologies;
        set => SetEnumLikeProperty(ref _symbologies, value, "Symbologies");
    }

    /// <summary>
    /// Selected Data Matrix/QR polarity.
    /// </summary>
    public string CodePolarity
    {
        get => _codePolarity;
        set => SetEnumLikeProperty(ref _codePolarity, value, "CodePolarity");
    }

    /// <summary>
    /// Selected search speed.
    /// </summary>
    public string CodeSearchSpeed
    {
        get => _codeSearchSpeed;
        set => SetEnumLikeProperty(ref _codeSearchSpeed, value, "CodeSearchSpeed");
    }

    /// <summary>
    /// Selected performance mode.
    /// </summary>
    public string PerformanceMode
    {
        get => _performanceMode;
        set => SetEnumLikeProperty(ref _performanceMode, value, "PerformanceMode");
    }

	[ObservableProperty]
	public partial int DetectorDensity { get; set; }

	[ObservableProperty]
	public partial int MaxCodes { get; set; }

	[ObservableProperty]
	public partial int TimeLimitMs { get; set; }

	[ObservableProperty]
	public partial bool BasicInkjetDpmEnabled { get; set; }

	[ObservableProperty]
	public partial int ResultCount { get; set; }

	[ObservableProperty]
	public partial string TypeDescription { get; set; } = "No results";

	[ObservableProperty]
	public partial string DisplayText { get; set; } = string.Empty;

    /// <inheritdoc/>
    public override string? Summary => ResultCount > 0
        ? $"{ResultCount} code(s)"
        : $"{Symbologies} / no codes";

    /// <inheritdoc/>
    public override bool IsEditableWhileRunning => true;

    partial void OnDetectorDensityChanged(int value)
    {
        DetectorDensity = Math.Clamp(value, 1, 4);
        SetInt("DetectorDensity", DetectorDensity);
        RaiseSummaryChanged();
    }

    partial void OnMaxCodesChanged(int value)
    {
        MaxCodes = Math.Clamp(value, 0, 256);
        SetInt("MaxCodes", MaxCodes);
        RaiseSummaryChanged();
    }

    partial void OnTimeLimitMsChanged(int value)
    {
        TimeLimitMs = Math.Clamp(value, 0, 60000);
        SetInt("TimeLimitMs", TimeLimitMs);
        RaiseSummaryChanged();
    }

    partial void OnBasicInkjetDpmEnabledChanged(bool value)
    {
        SetBool("BasicInkjetDpmEnabled", value);
        RaiseSummaryChanged();
    }

    partial void OnResultCountChanged(int value)
    {
        RaiseSummaryChanged();
    }

    /// <inheritdoc/>
    public override void ApplyTextPreview(TextPreviewDto preview)
    {
        TypeDescription = preview.TypeDescription;
        DisplayText = preview.DisplayText;
        ResultCount = CountResultLines(preview.DisplayText);
        RaiseSummaryChanged();
    }

    private void SetEnumLikeProperty(ref string field, string? value, string propertyName)
    {
        var nextValue = value ?? string.Empty;
        if (string.IsNullOrWhiteSpace(nextValue) && !string.IsNullOrWhiteSpace(field))
            return;

        if (SetProperty(ref field, nextValue, propertyName))
        {
            SetString(propertyName, nextValue);
            RaiseSummaryChanged();
        }
    }

    private static int CountResultLines(string text)
        => string.IsNullOrWhiteSpace(text)
            ? 0
            : text.Split([Environment.NewLine], StringSplitOptions.None)
                .Count(line => line.StartsWith('#'));
}
