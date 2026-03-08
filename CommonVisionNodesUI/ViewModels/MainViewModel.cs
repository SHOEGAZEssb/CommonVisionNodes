using System.ComponentModel;
using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace CommonVisionNodesUI.ViewModels;

/// <summary>
/// Top-level view model for the main page. Coordinates the node graph,
/// property panel, toolbar state, and status bar metrics.
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly DispatcherTimer _statusTimer;
    private DateTime _lastCpuCheck;
    private TimeSpan _lastCpuTime;

    public NodeGraphViewModel Graph { get; } = new();

    // Properties panel
    [ObservableProperty]
    private string _selectedNodeTitle = string.Empty;

    [ObservableProperty]
    private string _selectedNodeTypeName = string.Empty;

    [ObservableProperty]
    private Visibility _propertiesPanelVisibility = Visibility.Collapsed;

    [ObservableProperty]
    private Visibility _noSelectionVisibility = Visibility.Visible;

    [ObservableProperty]
    private bool _isPropertiesContentEnabled = true;

    // Toolbar
    [ObservableProperty]
    private bool _isEditingEnabled = true;

    // Run button
    [ObservableProperty]
    private string _runButtonText = "\uE768 Run";

    [ObservableProperty]
    private SolidColorBrush _runButtonBackground = new(Color.FromArgb(255, 56, 142, 60));

    // Status bar
    [ObservableProperty]
    private string _fpsText = "—";

    [ObservableProperty]
    private string _cpuText = "0.0%";

    [ObservableProperty]
    private string _memoryText = "0 MB";

    public MainViewModel()
    {
        Graph.PropertyChanged += OnGraphPropertyChanged;

        var process = Process.GetCurrentProcess();
        _lastCpuCheck = DateTime.UtcNow;
        _lastCpuTime = process.TotalProcessorTime;

        _statusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _statusTimer.Tick += OnStatusTimerTick;
        _statusTimer.Start();
    }

    private void OnStatusTimerTick(object? sender, object e)
    {
        UpdateCpuAndMemory();
        FpsText = Graph.IsRunning ? Graph.Fps.ToString("F1") : "—";
    }

    private void UpdateCpuAndMemory()
    {
        var process = Process.GetCurrentProcess();
        var now = DateTime.UtcNow;
        var currentCpuTime = process.TotalProcessorTime;

        var elapsed = (now - _lastCpuCheck).TotalMilliseconds;
        var cpuDelta = (currentCpuTime - _lastCpuTime).TotalMilliseconds;

        _lastCpuCheck = now;
        _lastCpuTime = currentCpuTime;

        if (elapsed > 0)
        {
            var cpuPercent = cpuDelta / elapsed / Environment.ProcessorCount * 100.0;
            CpuText = $"{cpuPercent:F1}%";
        }

        var memoryMb = process.WorkingSet64 / (1024.0 * 1024.0);
        MemoryText = $"{memoryMb:F0} MB";
    }

    private void OnGraphPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(NodeGraphViewModel.SelectedNode):
                OnSelectedNodeChanged();
                break;
            case nameof(NodeGraphViewModel.IsRunning):
                OnRunningStateChanged();
                break;
        }
    }

    private void OnSelectedNodeChanged()
    {
        var selected = Graph.SelectedNode;
        if (selected != null)
        {
            SelectedNodeTitle = selected.Title;
            SelectedNodeTypeName = selected.Node.GetType().Name;
            PropertiesPanelVisibility = Visibility.Visible;
            NoSelectionVisibility = Visibility.Collapsed;
        }
        else
        {
            SelectedNodeTitle = string.Empty;
            SelectedNodeTypeName = string.Empty;
            PropertiesPanelVisibility = Visibility.Collapsed;
            NoSelectionVisibility = Visibility.Visible;
        }

        UpdatePropertiesEnabled();
    }

    private void OnRunningStateChanged()
    {
        bool running = Graph.IsRunning;
        IsEditingEnabled = !running;
        RunButtonText = running ? "\uE71A Stop" : "\uE768 Run";
        RunButtonBackground = new SolidColorBrush(running
            ? Color.FromArgb(255, 211, 47, 47)
            : Color.FromArgb(255, 56, 142, 60));
        UpdatePropertiesEnabled();
    }

    private void UpdatePropertiesEnabled()
    {
        IsPropertiesContentEnabled = !Graph.IsRunning
            || (Graph.SelectedNode?.IsEditableWhileRunning ?? false);
    }

    [RelayCommand]
    private void RemoveSelectedNode()
    {
        if (Graph.SelectedNode is { } node)
        {
            Graph.SelectNode(null);
            Graph.RemoveNodeCommand.Execute(node);
        }
    }
}
