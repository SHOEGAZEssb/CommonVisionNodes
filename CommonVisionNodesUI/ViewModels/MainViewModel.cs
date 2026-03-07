using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace CommonVisionNodesUI.ViewModels;

public partial class MainViewModel : ObservableObject
{
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

    public MainViewModel()
    {
        Graph.PropertyChanged += OnGraphPropertyChanged;
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
