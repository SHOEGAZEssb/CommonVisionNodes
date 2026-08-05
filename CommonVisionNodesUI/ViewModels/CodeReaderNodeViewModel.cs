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

		_symbologies = GetString("Symbologies", FirstOptionOrDefault(_availableSymbologies));
		_codePolarity = GetString("CodePolarity", FirstOptionOrDefault(_availablePolarities));
		_codeSearchSpeed = GetString("CodeSearchSpeed", FirstOptionOrDefault(_availableSearchSpeeds));
		_performanceMode = GetString("PerformanceMode", FirstOptionOrDefault(_availablePerformanceModes));

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
		set => SetOptionValue(ref _symbologies, value, nameof(Symbologies));
	}

	/// <summary>
	/// Selected Data Matrix/QR polarity.
	/// </summary>
	public string CodePolarity
	{
		get => _codePolarity;
		set => SetOptionValue(ref _codePolarity, value, nameof(CodePolarity));
	}

	/// <summary>
	/// Selected search speed.
	/// </summary>
	public string CodeSearchSpeed
	{
		get => _codeSearchSpeed;
		set => SetOptionValue(ref _codeSearchSpeed, value, nameof(CodeSearchSpeed));
	}

	/// <summary>
	/// Selected performance mode.
	/// </summary>
	public string PerformanceMode
	{
		get => _performanceMode;
		set => SetOptionValue(ref _performanceMode, value, nameof(PerformanceMode));
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
	public partial IReadOnlyList<CodeReaderResultDto> Results { get; set; } = [];

	[ObservableProperty]
	public partial bool TimeLimitReached { get; set; }

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
	public override void ApplyCodeReaderPreview(CodeReaderPreviewDto preview)
	{
		PreviewImage = preview.Image;
		Results = [.. preview.Results];
		TimeLimitReached = preview.TimeLimitReached;
		ResultCount = Results.Count;
		TypeDescription = Results.Count == 0 ? "No codes" : "CodeReader[]";
		DisplayText = FormatResults(Results, TimeLimitReached);
		RaiseSummaryChanged();
	}

	private static string FormatResults(IReadOnlyList<CodeReaderResultDto> results, bool timeLimitReached)
	{
		if (results.Count == 0)
			return timeLimitReached ? "No codes found before time limit." : "No codes found.";

		var lines = results.Select(result =>
		{
			var qualityText = result.Quality.HasValue ? $" q={result.Quality.Value}" : string.Empty;
			return $"#{result.Index} {result.Symbology} {result.DecodeStatus} center=({result.CenterX:F0},{result.CenterY:F0}){qualityText} data={result.Data}";
		});

		var text = string.Join(Environment.NewLine, lines);
		return timeLimitReached
			? $"{text}{Environment.NewLine}Time limit reached."
			: text;
	}
}
