using CommonVisionNodesUI.Controls;
using CommonVisionNodesUI.ViewModels;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;

namespace CommonVisionNodesUI;

public sealed partial class MainPage : Page
{
    private readonly MainViewModel _viewModel = new();
    private readonly List<Path> _connectionPaths = [];
    private readonly Dictionary<NodeViewModel, NodeControl> _nodeControls = [];

    private PortViewModel? _connectionDragSource;
    private Path? _pendingConnectionPath;
    private NodeControl? _selectedControl;

    public MainPage()
    {
        this.InitializeComponent();
        DataContext = _viewModel;

        _viewModel.Graph.Nodes.CollectionChanged += (_, e) =>
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

        _viewModel.Graph.Connections.CollectionChanged += (_, _) => RedrawConnections();
        _viewModel.Graph.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(NodeGraphViewModel.SelectedNode))
                UpdateSelectionVisual();
        };
    }

    // --- Node control management ---

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

    // --- Selection visual ---

    private void OnNodeSelected(NodeControl control)
    {
        _viewModel.Graph.SelectNode(control.ViewModel);
    }

    private void UpdateSelectionVisual()
    {
        _selectedControl?.SetSelected(false);

        var selected = _viewModel.Graph.SelectedNode;
        if (selected != null && _nodeControls.TryGetValue(selected, out var control))
        {
            control.SetSelected(true);
            _selectedControl = control;
        }
        else
        {
            _selectedControl = null;
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
        if (e.OriginalSource == GraphCanvas)
            _viewModel.Graph.SelectNode(null);
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
                _viewModel.Graph.TryConnect(_connectionDragSource, targetPort);
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
        foreach (var nodeVM in _viewModel.Graph.Nodes)
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

        foreach (var conn in _viewModel.Graph.Connections)
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

    // --- Keyboard ---

    private void GraphCanvas_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Delete && _viewModel.IsEditingEnabled)
        {
            _viewModel.RemoveSelectedNodeCommand.Execute(null);
            e.Handled = true;
        }
    }

    // --- Code generation ---

    private async void GenerateCodeButton_Click(object sender, RoutedEventArgs e)
    {
        var code = _viewModel.Graph.GenerateCode();

        var codeBox = new TextBox
        {
            Text = code,
            IsReadOnly = true,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.NoWrap,
            FontFamily = new FontFamily("Consolas"),
            FontSize = 12,
            Height = 400
        };
        ScrollViewer.SetVerticalScrollBarVisibility(codeBox, ScrollBarVisibility.Auto);
        ScrollViewer.SetHorizontalScrollBarVisibility(codeBox, ScrollBarVisibility.Auto);

        var dialog = new ContentDialog
        {
            Title = "Generated CVB SDK Code",
            Content = codeBox,
            PrimaryButtonText = "Copy to Clipboard",
            CloseButtonText = "Close",
            XamlRoot = this.XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            var dataPackage = new DataPackage();
            dataPackage.SetText(code);
            Clipboard.SetContent(dataPackage);
        }
    }
}
