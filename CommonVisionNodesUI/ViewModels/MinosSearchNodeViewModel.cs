using System.Globalization;
using System.IO;
using CommonVisionNodes.Contracts;
using Microsoft.UI.Xaml;

namespace CommonVisionNodesUI.ViewModels;

/// <summary>
/// View model for a Minos pattern-search node.
/// </summary>
public partial class MinosSearchNodeViewModel : NodeViewModel
{
    private readonly IReadOnlyList<string> _availableSearchOperations;
    private string _searchOperation = string.Empty;

    /// <summary>
    /// Creates a Minos search node view model.
    /// </summary>
    /// <param name="node">Serialized node instance.</param>
    /// <param name="definition">Catalog definition.</param>
    public MinosSearchNodeViewModel(NodeDto node, NodeDefinitionDto definition)
        : base(node, definition)
    {
        _availableSearchOperations = [.. GetOptions("SearchOperation").Select(option => option.Value)];
        _searchOperation = GetString("SearchOperation", _availableSearchOperations.FirstOrDefault() ?? "FindAll");
        ClassifierPath = GetString("ClassifierPath");
        Density = GetDouble("Density", 1.0);
        MinQuality = GetDouble("MinQuality", 0.5);
        Locality = GetInt("Locality", 10);
        MaxResults = GetInt("MaxResults", 100);
    }

    /// <summary>
    /// Available Minos search operations.
    /// </summary>
    public IReadOnlyList<string> AvailableSearchOperations => _availableSearchOperations;

    /// <summary>
    /// Selected Minos search operation.
    /// </summary>
    public string SearchOperation
    {
        get => _searchOperation;
        set
        {
            var nextValue = value ?? string.Empty;
            if (string.IsNullOrWhiteSpace(nextValue) && !string.IsNullOrWhiteSpace(_searchOperation))
                return;

            if (SetProperty(ref _searchOperation, nextValue))
            {
                SetString("SearchOperation", nextValue);
                RaiseSummaryChanged();
            }
        }
    }

    [ObservableProperty]
    public partial string ClassifierPath { get; set; } = string.Empty;

    [ObservableProperty]
    public partial double Density { get; set; }

    [ObservableProperty]
    public partial double MinQuality { get; set; }

    /// <summary>
    /// Density formatted for the slider readout without floating-point artifacts.
    /// </summary>
    public string DensityDisplay => Density.ToString("0.##", CultureInfo.InvariantCulture);

    /// <summary>
    /// Minimum quality formatted for the slider readout without floating-point artifacts.
    /// </summary>
    public string MinQualityDisplay => MinQuality.ToString("0.##", CultureInfo.InvariantCulture);

    [ObservableProperty]
    public partial int Locality { get; set; }

    [ObservableProperty]
    public partial int MaxResults { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<ClassificationResultDto> Results { get; set; } = [];

    [ObservableProperty]
    public partial ImagePreviewDto? PreviewImage { get; set; }

    /// <summary>
    /// Number of Minos matches in the latest preview.
    /// </summary>
    public int ResultCount => Results.Count;

    /// <summary>
    /// Classifier files are loaded during node initialization and cannot be swapped live.
    /// </summary>
    public bool IsClassifierPathEditable => !IsGraphRunning;

    /// <summary>
    /// Shows the classifier-path runtime lock hint while execution is active.
    /// </summary>
    public Visibility ClassifierPathRuntimeLockVisibility => IsGraphRunning ? Visibility.Visible : Visibility.Collapsed;

    /// <inheritdoc/>
    public override string? Summary => string.IsNullOrEmpty(ClassifierPath)
        ? "No classifier loaded"
        : $"{Path.GetFileName(ClassifierPath)} ({ResultCount} match(es))";

    /// <inheritdoc/>
    public override bool IsEditableWhileRunning => true;

    /// <inheritdoc/>
    protected override void OnRuntimeEditStateChanged()
    {
        OnPropertyChanged(nameof(IsClassifierPathEditable));
        OnPropertyChanged(nameof(ClassifierPathRuntimeLockVisibility));
    }

    partial void OnClassifierPathChanged(string value)
    {
        SetString("ClassifierPath", value);
        RaiseSummaryChanged();
    }

    partial void OnDensityChanged(double value)
    {
        Density = double.IsFinite(value) ? Math.Clamp(value, 0.0, 1.0) : 1.0;
        SetDouble("Density", Density);
        OnPropertyChanged(nameof(DensityDisplay));
    }

    partial void OnMinQualityChanged(double value)
    {
        MinQuality = double.IsFinite(value) ? Math.Clamp(value, 0.0, 1.0) : 0.5;
        SetDouble("MinQuality", MinQuality);
        OnPropertyChanged(nameof(MinQualityDisplay));
    }

    partial void OnLocalityChanged(int value)
    {
        Locality = Math.Max(0, value);
        SetInt("Locality", Locality);
    }

    partial void OnMaxResultsChanged(int value)
    {
        MaxResults = Math.Max(0, value);
        SetInt("MaxResults", MaxResults);
    }

    /// <inheritdoc/>
    public override void ApplyClassificationPreview(ClassificationPreviewDto preview)
    {
        PreviewImage = preview.Image;
        Results = [.. preview.Results];
        OnPropertyChanged(nameof(ResultCount));
        RaiseSummaryChanged();
    }
}
