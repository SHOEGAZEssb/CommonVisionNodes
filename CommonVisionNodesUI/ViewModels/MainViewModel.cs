using System.ComponentModel;
using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace CommonVisionNodesUI.ViewModels;

/// <summary>
/// Root view model for shell state, selected-node panel state, and status metrics.
/// </summary>
public partial class MainViewModel : ObservableObject
{
	private readonly DispatcherTimer _statusTimer;
	private DateTime _lastCpuCheck;
	private TimeSpan _lastCpuTime;

	/// <summary>
	/// Creates the main view model.
	/// </summary>
	/// <param name="graph">Graph editor view model.</param>
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

	/// <summary>
	/// Graph editor view model shown by the main page.
	/// </summary>
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
	public partial Visibility RuntimeEditNoticeVisibility { get; set; } = Visibility.Collapsed;

	[ObservableProperty]
	public partial string RuntimeEditNoticeText { get; set; } = string.Empty;

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
				// Process.TotalProcessorTime is accumulated across all logical processors, so
				// divide by processor count to produce a familiar task-manager style percentage.
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
		var selected = Graph.SelectedNode;
		var locked = Graph.IsRunning && selected is not null && !selected.IsEditableWhileRunning;

		IsPropertiesContentEnabled = !locked;
		RuntimeEditNoticeVisibility = locked ? Visibility.Visible : Visibility.Collapsed;
		RuntimeEditNoticeText = locked ? selected!.RuntimeEditLockMessage : string.Empty;
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
