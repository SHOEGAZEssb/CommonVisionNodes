using System.Collections.Specialized;
using CommonVisionNodes.Contracts;
using CommonVisionNodesUI.Controls;
using CommonVisionNodesUI.Services;
using CommonVisionNodesUI.ViewModels;
using Microsoft.UI.Input;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Provider;

namespace CommonVisionNodesUI;

/// <summary>
/// Main graph editor page.
/// </summary>
public sealed partial class MainPage : Page
{
	private static readonly string[] ImageFileExtensions =
	[
		".bmp", ".dib", ".jpg", ".jpeg", ".png", ".tif", ".tiff", ".gif"
	];
	private static readonly string[] GraphFileExtensions = [".cvbgraph"];

	private readonly MainViewModel _viewModel;
#if __WASM__
	private readonly IBackendClient _backendClient;
#endif
	private readonly Dictionary<ConnectionViewModel, Path> _connectionPaths = [];
	private readonly Dictionary<NodeViewModel, NodeControl> _nodeControls = [];
	private readonly HashSet<NodeViewModel> _nodesWithPendingConnectionUpdates = [];
	private readonly Dictionary<NodeViewModel, HashSet<ConnectionViewModel>> _connectionsByNode = [];
	private readonly HashSet<ConnectionViewModel> _connectionsToUpdate = [];

	private PortViewModel? _connectionDragSource;
	private Path? _pendingConnectionPath;
	private NodeControl? _selectedControl;
	private readonly InputCursor? _propertiesPanelResizeCursor;

	private bool _isPanning;
	private bool _panHasMoved;
	private bool _isResizingPropertiesPanel;
	private bool _isPageLoaded;
	private bool _isGraphRenderScheduled;
	private bool _gridRedrawPending;
	private bool _connectionSynchronizationPending;
	private Point _panStart;
	private Point _propertiesPanelResizeStart;
	private double _panStartTranslateX;
	private double _panStartTranslateY;
	private double _propertiesPanelResizeStartWidth;

	private const double MinZoom = 0.1;
	private const double MaxZoom = 3.0;
	private const double ZoomFactor = 1.1;
	private const double MinPropertiesPanelWidth = 240;
	private const double MaxPropertiesPanelWidth = 560;

	/// <summary>
	/// Creates the main page and wires graph editor interactions.
	/// </summary>
	public MainPage()
	{
		this.InitializeComponent();
		_propertiesPanelResizeCursor = InputSystemCursor.Create(InputSystemCursorShape.SizeWestEast);

		_viewModel = ((App)Application.Current).Host!.Services.GetRequiredService<MainViewModel>();
#if __WASM__
		_backendClient = ((App)Application.Current).Host!.Services.GetRequiredService<IBackendClient>();
#endif
		DataContext = _viewModel;

		Loaded += async (_, _) =>
		{
			_isPageLoaded = true;
			await _viewModel.Graph.InitializeAsync();
			ScheduleGraphRender();
		};
		Unloaded += (_, _) =>
		{
			_isPageLoaded = false;
			CancelScheduledGraphRender();
		};

		GraphCanvasContainer.SizeChanged += (_, e) =>
		{
			GraphCanvasContainer.Clip = new RectangleGeometry
			{
				Rect = new Rect(0, 0, e.NewSize.Width, e.NewSize.Height)
			};
			RequestGridRedraw();
		};

		_viewModel.Graph.Nodes.CollectionChanged += (_, e) =>
		{
			if (e.Action == NotifyCollectionChangedAction.Reset)
			{
				ClearNodeControls();
				return;
			}

			if (e.NewItems is not null)
			{
				foreach (NodeViewModel nodeViewModel in e.NewItems)
					AddNodeControl(nodeViewModel);
			}

			if (e.OldItems is not null)
			{
				foreach (NodeViewModel nodeViewModel in e.OldItems)
					RemoveNodeControl(nodeViewModel);
			}
		};

		_viewModel.Graph.Connections.CollectionChanged += (_, _) => RequestConnectionSynchronization();
		_viewModel.Graph.PropertyChanged += (_, e) =>
		{
			if (e.PropertyName == nameof(NodeGraphViewModel.SelectedNode))
				UpdateSelectionVisual();
		};
	}

	/// <summary>
	/// View model bound to the page.
	/// </summary>
	public MainViewModel ViewModel => _viewModel;

	private void AddNodeControl(NodeViewModel nodeViewModel)
	{
		var control = new NodeControl();
		control.SetViewModel(nodeViewModel);
		control.NodeMoved += OnNodeMoved;
		control.PortPressed += OnPortPressed;
		control.PortRightTapped += OnPortRightTapped;
		control.NodeSelected += OnNodeSelected;
		_nodeControls[nodeViewModel] = control;
		GraphCanvas.Children.Add(control);
	}

	private void RemoveNodeControl(NodeViewModel nodeViewModel)
	{
		if (_nodeControls.TryGetValue(nodeViewModel, out var control))
		{
			GraphCanvas.Children.Remove(control);
			_nodeControls.Remove(nodeViewModel);
		}
	}

	private void ClearNodeControls()
	{
		foreach (var control in _nodeControls.Values)
			GraphCanvas.Children.Remove(control);

		_nodeControls.Clear();
		_selectedControl = null;
		_nodesWithPendingConnectionUpdates.Clear();
		_connectionsToUpdate.Clear();
		_connectionSynchronizationPending = false;
		ClearConnectionPaths();
	}

	private void OnNodeMoved(NodeControl control)
	{
		if (control.ViewModel is not null)
			RequestConnectionUpdate(control.ViewModel);
	}

	private void OnNodeSelected(NodeControl control)
	{
		CommitPropertyEditorChanges();
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

	private void OnPortRightTapped(NodeControl sender, PortViewModel port)
	{
		_viewModel.Graph.DisconnectPort(port);
	}

	private void GraphCanvas_PointerPressed(object sender, PointerRoutedEventArgs e)
	{
		if (e.OriginalSource == GraphCanvas)
		{
			_isPanning = true;
			_panHasMoved = false;
			_panStart = e.GetCurrentPoint(this).Position;
			_panStartTranslateX = CanvasTransform.TranslateX;
			_panStartTranslateY = CanvasTransform.TranslateY;
			GraphCanvas.CapturePointer(e.Pointer);
		}
	}

	private void GraphCanvas_PointerMoved(object sender, PointerRoutedEventArgs e)
	{
		if (_connectionDragSource != null && _pendingConnectionPath != null)
		{
			var pos = e.GetCurrentPoint(GraphCanvas).Position;
			UpdateBezierPath(_pendingConnectionPath,
				_connectionDragSource.CenterX, _connectionDragSource.CenterY,
				pos.X, pos.Y);
		}
		else if (_isPanning)
		{
			var current = e.GetCurrentPoint(this).Position;
			var dx = current.X - _panStart.X;
			var dy = current.Y - _panStart.Y;
			if (Math.Abs(dx) > 2 || Math.Abs(dy) > 2)
				_panHasMoved = true;
			CanvasTransform.TranslateX = _panStartTranslateX + dx;
			CanvasTransform.TranslateY = _panStartTranslateY + dy;
			RequestGridRedraw();
		}
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
		NodeControl.IsConnectionDragging = false;

		if (_isPanning && !_panHasMoved)
		{
			CommitPropertyEditorChanges();
			_viewModel.Graph.SelectNode(null);
		}
		_isPanning = false;

		GraphCanvas.ReleasePointerCaptures();
	}

	private PortViewModel? HitTestPort(Point point)
	{
		const double hitRadius = 15;
		foreach (var nodeViewModel in _viewModel.Graph.Nodes)
		{
			foreach (var port in nodeViewModel.InputPorts.Concat(nodeViewModel.OutputPorts))
			{
				var dx = point.X - port.CenterX;
				var dy = point.Y - port.CenterY;
				if (dx * dx + dy * dy <= hitRadius * hitRadius)
					return port;
			}
		}

		return null;
	}

	private void RequestGridRedraw()
	{
		_gridRedrawPending = true;
		ScheduleGraphRender();
	}

	private void RequestConnectionSynchronization()
	{
		_connectionSynchronizationPending = true;
		ScheduleGraphRender();
	}

	private void RequestConnectionUpdate(NodeViewModel node)
	{
		if (_connectionSynchronizationPending)
			return;

		_nodesWithPendingConnectionUpdates.Add(node);
		ScheduleGraphRender();
	}

	private void ScheduleGraphRender()
	{
		if (!_isPageLoaded || _isGraphRenderScheduled || XamlRoot is null)
			return;

		_isGraphRenderScheduled = true;
		CompositionTarget.Rendering += OnGraphRendering;
	}

	private void CancelScheduledGraphRender()
	{
		if (!_isGraphRenderScheduled)
			return;

		CompositionTarget.Rendering -= OnGraphRendering;
		_isGraphRenderScheduled = false;
	}

	private void OnGraphRendering(object? sender, object e)
	{
		CompositionTarget.Rendering -= OnGraphRendering;
		_isGraphRenderScheduled = false;

		if (_gridRedrawPending)
		{
			_gridRedrawPending = false;
			GridCanvas.SetViewTransform(
				CanvasTransform.ScaleX,
				CanvasTransform.TranslateX,
				CanvasTransform.TranslateY);
		}

		if (_connectionSynchronizationPending)
		{
			_connectionSynchronizationPending = false;
			_nodesWithPendingConnectionUpdates.Clear();
			SynchronizeConnectionPaths();
		}
		else if (_nodesWithPendingConnectionUpdates.Count > 0)
		{
			var movedNodes = _nodesWithPendingConnectionUpdates.ToArray();
			_nodesWithPendingConnectionUpdates.Clear();
			UpdateConnectionsForNodes(movedNodes);
		}
	}

	private void SynchronizeConnectionPaths()
	{
		var connections = _viewModel.Graph.Connections;
		foreach (var removedConnection in _connectionPaths.Keys.Where(connection => !connections.Contains(connection)).ToArray())
		{
			GraphCanvas.Children.Remove(_connectionPaths[removedConnection]);
			_connectionPaths.Remove(removedConnection);
			RemoveConnectionFromNodeIndex(removedConnection);
		}

		foreach (var connection in connections)
		{
			if (!_connectionPaths.TryGetValue(connection, out var path))
			{
				path = CreateBezierPath(
					connection.Source.CenterX, connection.Source.CenterY,
					connection.Target.CenterX, connection.Target.CenterY,
					new SolidColorBrush(Windows.UI.Color.FromArgb(255, 144, 164, 174)));
				_connectionPaths.Add(connection, path);
				AddConnectionToNodeIndex(connection);
				GraphCanvas.Children.Insert(0, path);
			}
			else
			{
				UpdateConnectionPath(path, connection);
			}
		}
	}

	private void UpdateConnectionsForNodes(IReadOnlyCollection<NodeViewModel> movedNodes)
	{
		foreach (var node in movedNodes)
		{
			if (_connectionsByNode.TryGetValue(node, out var connections))
				_connectionsToUpdate.UnionWith(connections);
		}

		foreach (var connection in _connectionsToUpdate)
		{
			if (_connectionPaths.TryGetValue(connection, out var path))
				UpdateConnectionPath(path, connection);
		}

		_connectionsToUpdate.Clear();
	}

	private static void UpdateConnectionPath(Path path, ConnectionViewModel connection)
		=> UpdateBezierPath(
			path,
			connection.Source.CenterX,
			connection.Source.CenterY,
			connection.Target.CenterX,
			connection.Target.CenterY);

	private void ClearConnectionPaths()
	{
		foreach (var path in _connectionPaths.Values)
			GraphCanvas.Children.Remove(path);

		_connectionPaths.Clear();
		_connectionsByNode.Clear();
		_connectionsToUpdate.Clear();
	}

	private void AddConnectionToNodeIndex(ConnectionViewModel connection)
	{
		AddConnectionToNodeIndex(connection.Source.ParentNode, connection);
		if (!ReferenceEquals(connection.Source.ParentNode, connection.Target.ParentNode))
			AddConnectionToNodeIndex(connection.Target.ParentNode, connection);
	}

	private void AddConnectionToNodeIndex(NodeViewModel node, ConnectionViewModel connection)
	{
		if (!_connectionsByNode.TryGetValue(node, out var connections))
		{
			connections = [];
			_connectionsByNode.Add(node, connections);
		}

		connections.Add(connection);
	}

	private void RemoveConnectionFromNodeIndex(ConnectionViewModel connection)
	{
		RemoveConnectionFromNodeIndex(connection.Source.ParentNode, connection);
		if (!ReferenceEquals(connection.Source.ParentNode, connection.Target.ParentNode))
			RemoveConnectionFromNodeIndex(connection.Target.ParentNode, connection);
	}

	private void RemoveConnectionFromNodeIndex(NodeViewModel node, ConnectionViewModel connection)
	{
		if (!_connectionsByNode.TryGetValue(node, out var connections))
			return;

		connections.Remove(connection);
		if (connections.Count == 0)
			_connectionsByNode.Remove(node);
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
		if (path.Data is PathGeometry geometry && geometry.Figures.Count > 0)
		{
			var figure = geometry.Figures[0];
			figure.StartPoint = new Point(x1, y1);
			if (figure.Segments.Count > 0 && figure.Segments[0] is BezierSegment bezier)
			{
				bezier.Point1 = new Point(x1 + offset, y1);
				bezier.Point2 = new Point(x2 - offset, y2);
				bezier.Point3 = new Point(x2, y2);
			}
		}
	}

	private void GraphCanvas_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
	{
		var point = e.GetCurrentPoint(GraphCanvas);
		var delta = point.Properties.MouseWheelDelta;
		var factor = delta > 0 ? ZoomFactor : 1.0 / ZoomFactor;

		var oldScale = CanvasTransform.ScaleX;
		var newScale = Math.Clamp(oldScale * factor, MinZoom, MaxZoom);
		var scaleDelta = newScale - oldScale;

		var canvasPos = point.Position;
		CanvasTransform.TranslateX -= canvasPos.X * scaleDelta;
		CanvasTransform.TranslateY -= canvasPos.Y * scaleDelta;
		CanvasTransform.ScaleX = newScale;
		CanvasTransform.ScaleY = newScale;

		RequestGridRedraw();
		e.Handled = true;
	}

	private void GraphCanvas_KeyDown(object sender, KeyRoutedEventArgs e)
	{
		if (e.Key == Windows.System.VirtualKey.Delete && _viewModel.IsEditingEnabled)
		{
			_viewModel.RemoveSelectedNodeCommand.Execute(null);
			e.Handled = true;
		}
	}

	private void PropertiesPanelSplitter_PointerPressed(object sender, PointerRoutedEventArgs e)
	{
		_isResizingPropertiesPanel = true;
		ProtectedCursor = _propertiesPanelResizeCursor;
		_propertiesPanelResizeStart = e.GetCurrentPoint(this).Position;
		_propertiesPanelResizeStartWidth = PropertiesPanel.Width;
		((UIElement)sender).CapturePointer(e.Pointer);
		e.Handled = true;
	}

	private void PropertiesPanelSplitter_PointerEntered(object sender, PointerRoutedEventArgs e)
	{
		ProtectedCursor = _propertiesPanelResizeCursor;
	}

	private void PropertiesPanelSplitter_PointerExited(object sender, PointerRoutedEventArgs e)
	{
		if (!_isResizingPropertiesPanel)
			ProtectedCursor = null;
	}

	private void PropertiesPanelSplitter_PointerMoved(object sender, PointerRoutedEventArgs e)
	{
		if (!_isResizingPropertiesPanel)
			return;

		var current = e.GetCurrentPoint(this).Position;
		var deltaX = current.X - _propertiesPanelResizeStart.X;
		PropertiesPanel.Width = Math.Clamp(
			_propertiesPanelResizeStartWidth - deltaX,
			MinPropertiesPanelWidth,
			MaxPropertiesPanelWidth);
		RequestGridRedraw();
		e.Handled = true;
	}

	private void PropertiesPanelSplitter_PointerReleased(object sender, PointerRoutedEventArgs e)
	{
		if (!_isResizingPropertiesPanel)
			return;

		_isResizingPropertiesPanel = false;
		ProtectedCursor = null;
		((UIElement)sender).ReleasePointerCapture(e.Pointer);
		e.Handled = true;
	}

	private async void GenerateCodeButton_Click(object sender, RoutedEventArgs e)
	{
		var code = await _viewModel.Graph.GenerateCodeAsync();

		var codeBlock = new TextBlock
		{
			Text = code,
			IsTextSelectionEnabled = true,
			TextWrapping = TextWrapping.NoWrap,
			FontFamily = new FontFamily("Consolas"),
			FontSize = 12
		};

		var scrollViewer = new ScrollViewer
		{
			Content = codeBlock,
			MaxHeight = 400,
			HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
			VerticalScrollBarVisibility = ScrollBarVisibility.Auto
		};

		var dialog = new ContentDialog
		{
			Title = "Generated CVB SDK Code",
			Content = scrollViewer,
			PrimaryButtonText = "Copy to Clipboard",
			CloseButtonText = "Close",
			XamlRoot = XamlRoot,
			MaxWidth = 800
		};

		var result = await dialog.ShowAsync();
		if (result == ContentDialogResult.Primary)
		{
			var dataPackage = new DataPackage();
			dataPackage.SetText(code);
			Clipboard.SetContent(dataPackage);
		}
	}

	private async void BrowseImageFileButton_Click(object sender, RoutedEventArgs e)
	{
		if ((sender as FrameworkElement)?.DataContext is not ImageNodeViewModel viewModel)
			return;

		var path = await PickOpenFilePathAsync("Open Image File", viewModel.FilePath, ImageFileExtensions);
		if (path is not null)
			viewModel.FilePath = path;
	}

	private async void BrowseImageFolderButton_Click(object sender, RoutedEventArgs e)
	{
		if ((sender as FrameworkElement)?.DataContext is not ImageNodeViewModel viewModel)
			return;

		var path = await PickFolderPathAsync("Open Image Directory", viewModel.FilePath);
		if (path is not null)
			viewModel.FilePath = path;
	}

	private async void BrowseSaveImageFileButton_Click(object sender, RoutedEventArgs e)
	{
		if ((sender as FrameworkElement)?.DataContext is not SaveImageNodeViewModel viewModel)
			return;

		var path = await PickSaveFilePathAsync(
			"Select Output Image File",
			viewModel.FilePath,
			GetFileName(viewModel.FilePath, "output.bmp"),
			ImageFileExtensions);
		if (path is not null)
			viewModel.FilePath = path;
	}

	private async void BrowseClassifierFileButton_Click(object sender, RoutedEventArgs e)
	{
		var dataContext = (sender as FrameworkElement)?.DataContext;
		if (dataContext is not MinosSearchNodeViewModel and not PolimagoClassifyNodeViewModel)
			return;

		var currentPath = dataContext switch
		{
			MinosSearchNodeViewModel viewModel => viewModel.ClassifierPath,
			PolimagoClassifyNodeViewModel viewModel => viewModel.ClassifierPath,
			_ => string.Empty
		};
		var path = await PickOpenFilePathAsync("Open Classifier File", currentPath, [".clf"]);
		if (path is null)
			return;

		switch (dataContext)
		{
			case MinosSearchNodeViewModel minosViewModel:
				minosViewModel.ClassifierPath = path;
				break;
			case PolimagoClassifyNodeViewModel polimagoViewModel:
				polimagoViewModel.ClassifierPath = path;
				break;
		}
	}

	private async Task<string?> PickOpenFilePathAsync(
		string title,
		string? initialPath,
		IReadOnlyCollection<string> fileExtensions)
	{
#if __WASM__
		var result = await _backendClient.PickPathAsync(new PathPickerRequestDto
		{
			Mode = PathPickerModeDto.OpenFile,
			Title = title,
			InitialPath = initialPath,
			FileExtensions = [.. fileExtensions]
		});
		return result.Path;
#else
		var picker = new FileOpenPicker
		{
			SuggestedStartLocation = PickerLocationId.DocumentsLibrary
		};
		foreach (var extension in fileExtensions)
			picker.FileTypeFilter.Add(extension);

		InitializePicker(picker);
		return (await picker.PickSingleFileAsync())?.Path;
#endif
	}

	private async Task<string?> PickFolderPathAsync(string title, string? initialPath)
	{
#if __WASM__
		var result = await _backendClient.PickPathAsync(new PathPickerRequestDto
		{
			Mode = PathPickerModeDto.OpenFolder,
			Title = title,
			InitialPath = initialPath
		});
		return result.Path;
#else
		var picker = new FolderPicker
		{
			SuggestedStartLocation = PickerLocationId.PicturesLibrary
		};
		picker.FileTypeFilter.Add("*");
		InitializePicker(picker);
		return (await picker.PickSingleFolderAsync())?.Path;
#endif
	}

	private async Task<string?> PickSaveFilePathAsync(
		string title,
		string? initialPath,
		string suggestedFileName,
		IReadOnlyCollection<string> fileExtensions)
	{
#if __WASM__
		var result = await _backendClient.PickPathAsync(new PathPickerRequestDto
		{
			Mode = PathPickerModeDto.SaveFile,
			Title = title,
			InitialPath = initialPath,
			SuggestedFileName = suggestedFileName,
			FileExtensions = [.. fileExtensions]
		});
		return result.Path;
#else
		var picker = new FileSavePicker
		{
			SuggestedStartLocation = PickerLocationId.PicturesLibrary,
			SuggestedFileName = suggestedFileName
		};
		picker.FileTypeChoices.Add("Image", [.. fileExtensions]);
		InitializePicker(picker);
		return (await picker.PickSaveFileAsync())?.Path;
#endif
	}

	private static string GetFileName(string path, string fallback)
	{
		if (string.IsNullOrWhiteSpace(path))
			return fallback;

		var separatorIndex = Math.Max(path.LastIndexOf('/'), path.LastIndexOf('\\'));
		var fileName = path[(separatorIndex + 1)..];
		return string.IsNullOrWhiteSpace(fileName) ? fallback : fileName;
	}

	private async void SaveGraphButton_Click(object sender, RoutedEventArgs e)
	{
		var picker = new FileSavePicker
		{
			SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
			SuggestedFileName = "NodeGraph"
		};
		picker.FileTypeChoices.Add("Node Graph", GraphFileExtensions);
		InitializePicker(picker);

		var file = await picker.PickSaveFileAsync();
		if (file is null)
			return;

		CachedFileManager.DeferUpdates(file);

		var json = System.Text.Json.JsonSerializer.Serialize(
			_viewModel.Graph.ToGraphDto(),
			GraphFileJsonSerializerContext.Default.GraphDto);
		await FileIO.WriteTextAsync(file, json);

		await CachedFileManager.CompleteUpdatesAsync(file);
	}

	private async void LoadGraphButton_Click(object sender, RoutedEventArgs e)
	{
		var picker = new FileOpenPicker
		{
			SuggestedStartLocation = PickerLocationId.DocumentsLibrary
		};
		foreach (var extension in GraphFileExtensions)
			picker.FileTypeFilter.Add(extension);
		InitializePicker(picker);

		var file = await picker.PickSingleFileAsync();
		if (file is null)
			return;

		var json = await FileIO.ReadTextAsync(file);
		var graph = System.Text.Json.JsonSerializer.Deserialize(
			json,
			GraphFileJsonSerializerContext.Default.GraphDto);
		if (graph is null)
			return;

		var clearCurrentGraph = true;
		if (_viewModel.Graph.Nodes.Count > 0)
		{
			var clearGraphDialog = new ContentDialog
			{
				Title = "Clear current graph?",
				Content = "Do you want to clear the current graph before loading the selected graph?",
				PrimaryButtonText = "Yes",
				SecondaryButtonText = "No",
				CloseButtonText = "Cancel",
				XamlRoot = XamlRoot
			};

			var result = await clearGraphDialog.ShowAsync();
			if (result == ContentDialogResult.None)
				return;

			clearCurrentGraph = result == ContentDialogResult.Primary;
		}

		await _viewModel.Graph.LoadGraphAsync(graph, clearCurrentGraph);
	}

	private static void InitializePicker(object picker)
	{
		try
		{
			var app = (App)Application.Current;
			if (app.MainWindow is null)
				return;

			// Win32 file pickers must be associated with the native window handle. Other targets
			// do not need this, so failures are ignored below.
			var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(app.MainWindow);
			WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
		}
		catch
		{
			// Picker initialization is only needed on certain desktop targets.
		}
	}

	private void CommitPropertyEditorChanges()
	{
		GraphCanvas.Focus(FocusState.Programmatic);
	}

}
