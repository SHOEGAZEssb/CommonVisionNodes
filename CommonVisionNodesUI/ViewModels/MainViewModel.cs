using System.ComponentModel;
using System.Diagnostics;
using CommonVisionNodesUI.Helpers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace CommonVisionNodesUI.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly DispatcherTimer _statusTimer;
    private readonly GpuMonitor _gpuMonitor = new();
    private DateTime _lastCpuCheck;
    private TimeSpan _lastCpuTime;

    public MainViewModel(NodeGraphViewModel graph)
    {
        Graph = graph;
        Graph.PropertyChanged += OnGraphPropertyChanged;

        try
        {
            var process = Process.GetCurrentProcess();
            _lastCpuCheck = DateTime.UtcNow;
            _lastCpuTime = process.TotalProcessorTime;
        }
        catch
        {
            _lastCpuCheck = DateTime.UtcNow;
            _lastCpuTime = TimeSpan.Zero;
        }

        _statusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _statusTimer.Tick += OnStatusTimerTick;
        _statusTimer.Start();
    }

    public NodeGraphViewModel Graph { get; }

	[ObservableProperty]
	public partial string SelectedNodeTitle { get; set; } = string.Empty;

	[ObservableProperty]
	public partial string SelectedNodeTypeName { get; set; } = string.Empty;

	[ObservableProperty]
	public partial Visibility PropertiesPanelVisibility { get; set; } = Visibility.Collapsed;

	[ObservableProperty]
	public partial Visibility NoSelectionVisibility { get; set; } = Visibility.Visible;

	[ObservableProperty]
	public partial bool IsPropertiesContentEnabled { get; set; } = true;

	[ObservableProperty]
	public partial bool IsEditingEnabled { get; set; } = true;

	[ObservableProperty]
	public partial string RunButtonText { get; set; } = "\uE768 Run";

	[ObservableProperty]
	public partial SolidColorBrush RunButtonBackground { get; set; } = new(Color.FromArgb(255, 56, 142, 60));

	[ObservableProperty]
	public partial string FpsText { get; set; } = "-";

	[ObservableProperty]
	public partial string CpuText { get; set; } = "N/A";

	[ObservableProperty]
	public partial string MemoryText { get; set; } = "N/A";

	[ObservableProperty]
	public partial string GpuText { get; set; } = "N/A";

	private void OnStatusTimerTick(object? sender, object e)
    {
        UpdateCpuAndMemory();
        FpsText = Graph.IsRunning ? Graph.Fps.ToString("F1") : "-";
    }

    private void UpdateCpuAndMemory()
    {
        try
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
        catch
        {
            CpuText = "N/A";
            MemoryText = "N/A";
        }

        var gpuUtil = _gpuMonitor.GetUtilization();
        GpuText = gpuUtil.HasValue ? $"{gpuUtil.Value:F1}%" : "N/A";
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
            SelectedNodeTypeName = selected.Node.Type;
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
        var running = Graph.IsRunning;
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
