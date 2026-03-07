using CommonVisionNodesUI.Controls;
using CommonVisionNodesUI.ViewModels;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;

namespace CommonVisionNodesUI;

public sealed partial class MainPage : Page
{
    private readonly NodeGraphViewModel _viewModel = new();
    private readonly List<Path> _connectionPaths = [];
    private readonly Dictionary<NodeViewModel, NodeControl> _nodeControls = [];

    private PortViewModel? _connectionDragSource;
    private Path? _pendingConnectionPath;
    private NodeControl? _selectedControl;

    public MainPage()
    {
        this.InitializeComponent();

        _viewModel.Nodes.CollectionChanged += (_, e) =>
        {
            if (e.NewItems != null)
            {
                foreach (NodeViewModel nodeVM in e.NewItems)
                    AddNodeControl(nodeVM);
            }
            if (e.OldItems != null)
            {
                foreach (NodeViewModel nodeVM in e.OldItems)
                    RemoveNodeControl(nodeVM);
            }
        };

        _viewModel.Connections.CollectionChanged += (_, _) => RedrawConnections();
    }

    private void AddNodeControl(NodeViewModel nodeVM)
    {
        var control = new NodeControl();
        control.SetViewModel(nodeVM);
        control.NodeMoved += _ => RedrawConnections();
        control.PortPressed += OnPortPressed;
        control.NodeSelected += OnNodeSelected;
        _nodeControls[nodeVM] = control;
        GraphCanvas.Children.Add(control);
    }

    private void RemoveNodeControl(NodeViewModel nodeVM)
    {
        if (_nodeControls.TryGetValue(nodeVM, out var control))
        {
            GraphCanvas.Children.Remove(control);
            _nodeControls.Remove(nodeVM);
        }
    }

    // --- Selection ---

    private void OnNodeSelected(NodeControl control)
    {
        SelectNode(control.ViewModel);
    }

    private void SelectNode(NodeViewModel? nodeVM)
    {
        // Update old selection visual
        _selectedControl?.SetSelected(false);

        _viewModel.SelectNode(nodeVM);

        // Update new selection visual
        if (nodeVM != null && _nodeControls.TryGetValue(nodeVM, out var control))
        {
            control.SetSelected(true);
            _selectedControl = control;
        }
        else
        {
            _selectedControl = null;
        }

        UpdatePropertiesPanel();
    }

    private void UpdatePropertiesPanel()
    {
        var selected = _viewModel.SelectedNode;
        if (selected != null)
        {
            SelectedNodeTitle.Text = selected.Title;
            SelectedNodeType.Text = selected.Node.GetType().Name;

            PropertiesContent.Content = selected;
            PropertiesContent.ContentTemplate = selected switch
            {
                ImageNodeViewModel => (DataTemplate)Resources["ImageNodePropertiesTemplate"],
                SaveImageNodeViewModel => (DataTemplate)Resources["SaveImageNodePropertiesTemplate"],
                DeviceNodeViewModel => (DataTemplate)Resources["DeviceNodePropertiesTemplate"],
                BinarizeNodeViewModel => (DataTemplate)Resources["BinarizeNodePropertiesTemplate"],
                SubImageNodeViewModel => (DataTemplate)Resources["SubImageNodePropertiesTemplate"],
                _ => null
            };

            PropertiesPanel.Visibility = Visibility.Visible;
            NoSelectionText.Visibility = Visibility.Collapsed;
        }
        else
        {
            PropertiesContent.Content = null;
            PropertiesPanel.Visibility = Visibility.Collapsed;
            NoSelectionText.Visibility = Visibility.Visible;
        }
    }

    // --- Connection drag ---

    private void OnPortPressed(NodeControl sender, PortViewModel port, PointerRoutedEventArgs e)
    {
        _connectionDragSource = port;

        _pendingConnectionPath = CreateBezierPath(
            port.CenterX, port.CenterY,
            port.CenterX, port.CenterY,
            new SolidColorBrush(Colors.White));
        _pendingConnectionPath.Opacity = 0.6;
        _pendingConnectionPath.StrokeDashArray = [4, 2];
        GraphCanvas.Children.Add(_pendingConnectionPath);

        GraphCanvas.CapturePointer(e.Pointer);
    }

    private void GraphCanvas_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        // Click on canvas background deselects
        if (e.OriginalSource == GraphCanvas)
            SelectNode(null);
    }

    private void GraphCanvas_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_connectionDragSource == null || _pendingConnectionPath == null) return;

        var pos = e.GetCurrentPoint(GraphCanvas).Position;
        UpdateBezierPath(_pendingConnectionPath,
            _connectionDragSource.CenterX, _connectionDragSource.CenterY,
            pos.X, pos.Y);
    }

    private void GraphCanvas_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (_connectionDragSource != null)
        {
            var pos = e.GetCurrentPoint(GraphCanvas).Position;
            var targetPort = HitTestPort(pos);
            if (targetPort != null && targetPort != _connectionDragSource)
                _viewModel.TryConnect(_connectionDragSource, targetPort);
        }

        if (_pendingConnectionPath != null)
        {
            GraphCanvas.Children.Remove(_pendingConnectionPath);
            _pendingConnectionPath = null;
        }
        _connectionDragSource = null;
        GraphCanvas.ReleasePointerCaptures();
    }

    private PortViewModel? HitTestPort(Point point)
    {
        const double hitRadius = 15;
        foreach (var nodeVM in _viewModel.Nodes)
        {
            foreach (var port in nodeVM.InputPorts.Concat(nodeVM.OutputPorts))
            {
                var dx = point.X - port.CenterX;
                var dy = point.Y - port.CenterY;
                if (dx * dx + dy * dy <= hitRadius * hitRadius)
                    return port;
            }
        }
        return null;
    }

    // --- Connection drawing ---

    private void RedrawConnections()
    {
        foreach (var path in _connectionPaths)
            GraphCanvas.Children.Remove(path);
        _connectionPaths.Clear();

        foreach (var conn in _viewModel.Connections)
        {
            var path = CreateBezierPath(
                conn.Source.CenterX, conn.Source.CenterY,
                conn.Target.CenterX, conn.Target.CenterY,
                new SolidColorBrush(Windows.UI.Color.FromArgb(255, 144, 164, 174)));
            _connectionPaths.Add(path);
            GraphCanvas.Children.Insert(0, path);
        }
    }

    private static Path CreateBezierPath(double x1, double y1, double x2, double y2, Brush stroke)
    {
        var offset = Math.Max(50, Math.Abs(x2 - x1) * 0.4);
        var figure = new PathFigure { StartPoint = new Point(x1, y1) };
        figure.Segments.Add(new BezierSegment
        {
            Point1 = new Point(x1 + offset, y1),
            Point2 = new Point(x2 - offset, y2),
            Point3 = new Point(x2, y2)
        });
        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        return new Path
        {
            Data = geometry,
            Stroke = stroke,
            StrokeThickness = 2
        };
    }

    private static void UpdateBezierPath(Path path, double x1, double y1, double x2, double y2)
    {
        var offset = Math.Max(50, Math.Abs(x2 - x1) * 0.4);
        if (path.Data is PathGeometry geo && geo.Figures.Count > 0)
        {
            var figure = geo.Figures[0];
            figure.StartPoint = new Point(x1, y1);
            if (figure.Segments.Count > 0 && figure.Segments[0] is BezierSegment bezier)
            {
                bezier.Point1 = new Point(x1 + offset, y1);
                bezier.Point2 = new Point(x2 - offset, y2);
                bezier.Point3 = new Point(x2, y2);
            }
        }
    }

    // --- Toolbar handlers ---

    private void AddImageNode_Click(object sender, RoutedEventArgs e) =>
        _viewModel.AddImageNodeCommand.Execute(null);

    private void AddSaveImageNode_Click(object sender, RoutedEventArgs e) =>
        _viewModel.AddSaveImageNodeCommand.Execute(null);

    private void AddDeviceNode_Click(object sender, RoutedEventArgs e) =>
        _viewModel.AddDeviceNodeCommand.Execute(null);

    private void AddBinarizeNode_Click(object sender, RoutedEventArgs e) =>
        _viewModel.AddBinarizeNodeCommand.Execute(null);

    private void AddSubImageNode_Click(object sender, RoutedEventArgs e) =>
        _viewModel.AddSubImageNodeCommand.Execute(null);

    private void RemoveNode_Click(object sender, RoutedEventArgs e) =>
        RemoveSelectedNode();

    private void GraphCanvas_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Delete && !_viewModel.IsRunning)
        {
            RemoveSelectedNode();
            e.Handled = true;
        }
    }

    private void RemoveSelectedNode()
    {
        if (_viewModel.SelectedNode is { } node)
        {
            SelectNode(null);
            _viewModel.RemoveNodeCommand.Execute(node);
        }
    }

    private void Initialize_Click(object sender, RoutedEventArgs e) =>
        _viewModel.InitializeGraphCommand.Execute(null);

    private void Execute_Click(object sender, RoutedEventArgs e) =>
        _viewModel.ExecuteGraphCommand.Execute(null);

    private void ToggleRun_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.ToggleRunCommand.Execute(null);
        if (_viewModel.IsRunning)
        {
            RunStopButton.Content = "\uE71A Stop";
            RunStopButton.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 211, 47, 47));
        }
        else
        {
            RunStopButton.Content = "\uE768 Run";
            RunStopButton.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 56, 142, 60));
        }

        UpdateEditingEnabled();
    }

    private void UpdateEditingEnabled()
    {
        bool enabled = !_viewModel.IsRunning;
        AddImageButton.IsEnabled = enabled;
        AddSaveImageButton.IsEnabled = enabled;
        AddDeviceButton.IsEnabled = enabled;
        AddBinarizeButton.IsEnabled = enabled;
        AddSubImageButton.IsEnabled = enabled;
        InitializeButton.IsEnabled = enabled;
        ExecuteButton.IsEnabled = enabled;
        RemoveNodeButton.IsEnabled = enabled;
    }
}
