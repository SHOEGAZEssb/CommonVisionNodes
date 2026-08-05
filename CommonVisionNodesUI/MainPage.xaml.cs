using System.Text.Json;
using System.Text.Json.Serialization;
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

	private static readonly JsonSerializerOptions GraphJsonOptions = new(JsonSerializerDefaults.Web)
	{
		WriteIndented = true,
		DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
	};

	private readonly MainViewModel _viewModel;
#if __WASM__
	private readonly IBackendClient _backendClient;
#endif
	private readonly List<Path> _connectionPaths = [];
	private readonly Dictionary<NodeViewModel, NodeControl> _nodeControls = [];

	private PortViewModel? _connectionDragSource;
	private Path? _pendingConnectionPath;
	private NodeControl? _selectedControl;
	private readonly InputCursor? _propertiesPanelResizeCursor;

	private bool _isPanning;
	private bool _panHasMoved;
	private bool _isResizingPropertiesPanel;
	private Point _panStart;
	private Point _propertiesPanelResizeStart;
	private double _panStartTranslateX;
	private double _panStartTranslateY;
	private double _propertiesPanelResizeStartWidth;

	private const double MinZoom = 0.1;
	private const double MaxZoom = 3.0;
	private const double ZoomFactor = 1.1;
	private const double MinorGridSpacing = 25;
	private const double MajorGridSpacing = 100;
	private const double MinPropertiesPanelWidth = 240;
	private const double MaxPropertiesPanelWidth = 560;

	private readonly Path _minorGridPath = new()
	{
		Stroke = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 40, 40, 40)),
		StrokeThickness = 1,
		IsHitTestVisible = false
	};
	private readonly Path _majorGridPath = new()
	{
		Stroke = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 50, 50, 50)),
		StrokeThickness = 1,
		IsHitTestVisible = false
	};
	private readonly Path _originGridPath = new()
	{
		Stroke = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 65, 65, 65)),
		StrokeThickness = 1,
		IsHitTestVisible = false
	};

	static MainPage()
	{
		GraphJsonOptions.Converters.Add(new JsonStringEnumConverter());
	}

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

		Loaded += async (_, _) => await _viewModel.Graph.InitializeAsync();

		GraphCanvasContainer.SizeChanged += (_, e) =>
		{
			GraphCanvasContainer.Clip = new RectangleGeometry
			{
				Rect = new Rect(0, 0, e.NewSize.Width, e.NewSize.Height)
			};
			RedrawGrid();
		};

		GridCanvas.Children.Add(_minorGridPath);
		GridCanvas.Children.Add(_majorGridPath);
		GridCanvas.Children.Add(_originGridPath);

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

		_viewModel.Graph.Connections.CollectionChanged += (_, _) => RedrawConnections();
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
		control.NodeMoved += _ => RedrawConnections();
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
			RedrawGrid();
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

	private void RedrawConnections()
	{
		foreach (var path in _connectionPaths)
			GraphCanvas.Children.Remove(path);
		_connectionPaths.Clear();

		foreach (var connection in _viewModel.Graph.Connections)
		{
			var path = CreateBezierPath(
				connection.Source.CenterX, connection.Source.CenterY,
				connection.Target.CenterX, connection.Target.CenterY,
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

		RedrawGrid();
		e.Handled = true;
	}

	private void RedrawGrid()
	{
		var scale = CanvasTransform.ScaleX;
		var tx = CanvasTransform.TranslateX;
		var ty = CanvasTransform.TranslateY;
		var viewW = GraphCanvasContainer.ActualWidth;
		var viewH = GraphCanvasContainer.ActualHeight;
		if (viewW <= 0 || viewH <= 0 || scale <= 0) return;

		var minorGeo = new PathGeometry();
		var majorGeo = new PathGeometry();
		var originGeo = new PathGeometry();

		var screenMinorSpacing = MinorGridSpacing * scale;
		var showMinor = screenMinorSpacing >= 6;
		var canvasLeft = -tx / scale;
		var canvasTop = -ty / scale;
		var canvasRight = (viewW - tx) / scale;
		var canvasBottom = (viewH - ty) / scale;
		var spacing = showMinor ? MinorGridSpacing : MajorGridSpacing;

		var startX = Math.Floor(canvasLeft / spacing) * spacing;
		for (var cx = startX; cx <= canvasRight; cx += spacing)
		{
			var sx = cx * scale + tx;
			var geo = Math.Abs(cx) < 0.5 ? originGeo
					: (!showMinor || Math.Abs(cx % MajorGridSpacing) < 0.5) ? majorGeo
					: minorGeo;
			var fig = new PathFigure { StartPoint = new Point(sx, 0) };
			fig.Segments.Add(new LineSegment { Point = new Point(sx, viewH) });
			geo.Figures.Add(fig);
		}

		var startY = Math.Floor(canvasTop / spacing) * spacing;
		for (var cy = startY; cy <= canvasBottom; cy += spacing)
		{
			var sy = cy * scale + ty;
			var geo = Math.Abs(cy) < 0.5 ? originGeo
					: (!showMinor || Math.Abs(cy % MajorGridSpacing) < 0.5) ? majorGeo
					: minorGeo;
			var fig = new PathFigure { StartPoint = new Point(0, sy) };
			fig.Segments.Add(new LineSegment { Point = new Point(viewW, sy) });
			geo.Figures.Add(fig);
		}

		_minorGridPath.Data = minorGeo;
		_majorGridPath.Data = majorGeo;
		_originGridPath.Data = originGeo;
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
		RedrawGrid();
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

		var json = JsonSerializer.Serialize(_viewModel.Graph.ToGraphDto(), GraphJsonOptions);
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
		var graph = JsonSerializer.Deserialize<GraphDto>(json, GraphJsonOptions);
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
