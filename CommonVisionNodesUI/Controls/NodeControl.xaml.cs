using CommonVisionNodes.Contracts;
using CommonVisionNodesUI.ViewModels;
using Cvb.Uno.Toolkit.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;

namespace CommonVisionNodesUI.Controls;

/// <summary>
/// Interactive canvas control that renders a node, its ports, and inline previews.
/// </summary>
public sealed partial class NodeControl : UserControl
{
	private NodeViewModel? _viewModel;
	private bool _isSelected;
	private bool _isDragging;
	private bool _hasMoved;
	private bool _isResizing;
	private Point _dragStart;
	private Point _resizeStart;
	private double _startX;
	private double _startY;
	private double _startWidth;
	private double _startHeight;

	internal static bool IsConnectionDragging;

	/// <summary>
	/// Raised when the node is dragged to a new position.
	/// </summary>
	public event Action<NodeControl>? NodeMoved;

	/// <summary>
	/// Raised when the user presses a port to begin a connection gesture.
	/// </summary>
	public event Action<NodeControl, PortViewModel, PointerRoutedEventArgs>? PortPressed;

	/// <summary>
	/// Raised when the user right-clicks a port.
	/// </summary>
	public event Action<NodeControl, PortViewModel>? PortRightTapped;

	/// <summary>
	/// Raised when the user selects the node without dragging it.
	/// </summary>
	public event Action<NodeControl>? NodeSelected;

	/// <summary>
	/// View model currently rendered by the control.
	/// </summary>
	public NodeViewModel? ViewModel => _viewModel;

	/// <summary>
	/// Creates the node control.
	/// </summary>
	public NodeControl()
	{
		this.InitializeComponent();
	}

	/// <summary>
	/// Binds the control to a node view model and wires preview updates.
	/// </summary>
	/// <param name="vm">Node view model to render.</param>
	public void SetViewModel(NodeViewModel vm)
	{
		_viewModel = vm;
		TitleText.Text = vm.Title;
		HeaderBorder.Background = new SolidColorBrush(vm.HeaderColor);
		InputPortsList.ItemsSource = vm.InputPorts;
		OutputPortsList.ItemsSource = vm.OutputPorts;

		Canvas.SetLeft(this, vm.X);
		Canvas.SetTop(this, vm.Y);
		ApplyNodeSize();
		UpdateResizeGrip();

		vm.PropertyChanged += (_, e) =>
		{
			if (e.PropertyName == nameof(NodeViewModel.Summary))
				UpdateSummary();
			else if (e.PropertyName is nameof(NodeViewModel.ExecutionTime) or nameof(NodeViewModel.SinkFps))
				UpdateExecutionMetrics();
			else if (e.PropertyName is nameof(NodeViewModel.HasExecutionError) or nameof(NodeViewModel.ExecutionErrorText))
			{
				UpdateExecutionError();
				UpdateNodeBorder();
			}
			else if (e.PropertyName is nameof(NodeViewModel.Width) or nameof(NodeViewModel.Height))
			{
				ApplyNodeSize();
				NodeMoved?.Invoke(this);
			}
			else if (e.PropertyName == nameof(NodeViewModel.CanResize))
			{
				UpdateResizeGrip();
			}
		};
		UpdateSummary();
		UpdateExecutionMetrics();
		UpdateExecutionError();
		UpdateNodeBorder();

		if (vm is ImageNodeViewModel imageVM)
		{
			UpdateImagePreview(ImagePreview, vm, imageVM.PreviewImage);
			imageVM.PropertyChanged += (_, e) =>
			{
				if (e.PropertyName is nameof(ImageNodeViewModel.PreviewImage) or nameof(NodeViewModel.ShowPreview))
					UpdateImagePreview(ImagePreview, vm, imageVM.PreviewImage);
			};
		}
		else if (vm is DeviceNodeViewModel deviceVM)
		{
			UpdateImagePreview(ImagePreview, vm, deviceVM.PreviewImage);
			deviceVM.PropertyChanged += (_, e) =>
			{
				if (e.PropertyName is nameof(DeviceNodeViewModel.PreviewImage) or nameof(NodeViewModel.ShowPreview))
					UpdateImagePreview(ImagePreview, vm, deviceVM.PreviewImage);
			};
		}
		else if (vm is SaveImageNodeViewModel saveVM)
		{
			UpdateImagePreview(ImagePreview, vm, saveVM.PreviewImage);
			saveVM.PropertyChanged += (_, e) =>
			{
				if (e.PropertyName is nameof(SaveImageNodeViewModel.PreviewImage) or nameof(NodeViewModel.ShowPreview))
					UpdateImagePreview(ImagePreview, vm, saveVM.PreviewImage);
			};
		}
		else if (vm is GevServerNodeViewModel gevServerVM)
		{
			UpdateImagePreview(ImagePreview, vm, gevServerVM.PreviewImage);
			gevServerVM.PropertyChanged += (_, e) =>
			{
				if (e.PropertyName is nameof(GevServerNodeViewModel.PreviewImage) or nameof(NodeViewModel.ShowPreview))
					UpdateImagePreview(ImagePreview, vm, gevServerVM.PreviewImage);
			};
		}
		else if (vm is BinarizeNodeViewModel binarizeVM)
		{
			UpdateImagePreview(ImagePreview, vm, binarizeVM.PreviewImage);
			binarizeVM.PropertyChanged += (_, e) =>
			{
				if (e.PropertyName is nameof(BinarizeNodeViewModel.PreviewImage) or nameof(NodeViewModel.ShowPreview))
					UpdateImagePreview(ImagePreview, vm, binarizeVM.PreviewImage);
			};
		}
		else if (vm is SubImageNodeViewModel subVM)
		{
			UpdateCropPreview(vm, subVM);
			subVM.PropertyChanged += (_, e) =>
			{
				if (e.PropertyName is nameof(SubImageNodeViewModel.PreviewImage)
					or nameof(NodeViewModel.ShowPreview)
					or nameof(SubImageNodeViewModel.AreaX)
					or nameof(SubImageNodeViewModel.AreaY)
					or nameof(SubImageNodeViewModel.AreaWidth)
					or nameof(SubImageNodeViewModel.AreaHeight))
					UpdateCropPreview(vm, subVM);
			};
			CropPreview.CropAreaChanged += (x, y, w, h) =>
			{
				subVM.AreaX = x;
				subVM.AreaY = y;
				subVM.AreaWidth = w;
				subVM.AreaHeight = h;
			};
		}
		else if (vm is MatrixTransformNodeViewModel transformVM)
		{
			UpdateImagePreview(ImagePreview, vm, transformVM.PreviewImage);
			transformVM.PropertyChanged += (_, e) =>
			{
				if (e.PropertyName is nameof(MatrixTransformNodeViewModel.PreviewImage) or nameof(NodeViewModel.ShowPreview))
					UpdateImagePreview(ImagePreview, vm, transformVM.PreviewImage);
			};
		}
		else if (vm is ImageGeneratorNodeViewModel genVM)
		{
			UpdateImagePreview(ImagePreview, vm, genVM.PreviewImage);
			genVM.PropertyChanged += (_, e) =>
			{
				if (e.PropertyName is nameof(ImageGeneratorNodeViewModel.PreviewImage) or nameof(NodeViewModel.ShowPreview))
					UpdateImagePreview(ImagePreview, vm, genVM.PreviewImage);
			};
		}
		else if (vm is FilterNodeViewModel filterVM)
		{
			UpdateImagePreview(ImagePreview, vm, filterVM.PreviewImage);
			filterVM.PropertyChanged += (_, e) =>
			{
				if (e.PropertyName is nameof(FilterNodeViewModel.PreviewImage) or nameof(NodeViewModel.ShowPreview))
					UpdateImagePreview(ImagePreview, vm, filterVM.PreviewImage);
			};
		}
		else if (vm is MorphologyNodeViewModel morphVM)
		{
			UpdateImagePreview(ImagePreview, vm, morphVM.PreviewImage);
			morphVM.PropertyChanged += (_, e) =>
			{
				if (e.PropertyName is nameof(MorphologyNodeViewModel.PreviewImage) or nameof(NodeViewModel.ShowPreview))
					UpdateImagePreview(ImagePreview, vm, morphVM.PreviewImage);
			};
		}
		else if (vm is NormalizeNodeViewModel normalizeVM)
		{
			UpdateImagePreview(ImagePreview, vm, normalizeVM.PreviewImage);
			normalizeVM.PropertyChanged += (_, e) =>
			{
				if (e.PropertyName is nameof(NormalizeNodeViewModel.PreviewImage) or nameof(NodeViewModel.ShowPreview))
					UpdateImagePreview(ImagePreview, vm, normalizeVM.PreviewImage);
			};
		}
		else if (vm is HistogramNodeViewModel histVM)
		{
			HistogramPreview.Visibility = Visibility.Visible;
			histVM.PropertyChanged += (_, e) =>
			{
				if (e.PropertyName == nameof(HistogramNodeViewModel.Bins))
					HistogramPreview.SetHistogram(histVM.Bins, histVM.Mean, histVM.StdDev);
			};
		}
		else if (vm is BlobNodeViewModel blobVM)
		{
			UpdateBlobPreview(vm, blobVM);
			blobVM.PropertyChanged += (_, e) =>
			{
				if (e.PropertyName is nameof(BlobNodeViewModel.PreviewImage)
					or nameof(BlobNodeViewModel.Blobs)
					or nameof(NodeViewModel.ShowPreview))
					UpdateBlobPreview(vm, blobVM);
			};
		}
		else if (vm is MinosSearchNodeViewModel minosVM)
		{
			UpdateClassificationPreview(vm, minosVM.PreviewImage, minosVM.Results);
			minosVM.PropertyChanged += (_, e) =>
			{
				if (e.PropertyName is nameof(MinosSearchNodeViewModel.Results)
					or nameof(NodeViewModel.ShowPreview))
					UpdateClassificationPreview(vm, minosVM.PreviewImage, minosVM.Results);
			};
		}
		else if (vm is PolimagoClassifyNodeViewModel polimagoVM)
		{
			UpdateClassificationPreview(vm, polimagoVM.PreviewImage, polimagoVM.Results);
			polimagoVM.PropertyChanged += (_, e) =>
			{
				if (e.PropertyName is nameof(PolimagoClassifyNodeViewModel.Results)
					or nameof(NodeViewModel.ShowPreview))
					UpdateClassificationPreview(vm, polimagoVM.PreviewImage, polimagoVM.Results);
			};
		}
		else if (vm is CodeReaderNodeViewModel codeReaderVM)
		{
			UpdateCodeReaderPreview(vm, codeReaderVM);
			codeReaderVM.PropertyChanged += (_, e) =>
			{
				if (e.PropertyName is nameof(CodeReaderNodeViewModel.PreviewImage)
					or nameof(NodeViewModel.ShowPreview))
					UpdateCodeReaderPreviewImage(vm, codeReaderVM);

				if (e.PropertyName is nameof(CodeReaderNodeViewModel.Results)
					or nameof(CodeReaderNodeViewModel.TimeLimitReached)
					or nameof(NodeViewModel.ShowPreview))
					UpdateCodeReaderPreviewResults(vm, codeReaderVM);
			};
		}
		else if (vm is GenericVisualizerNodeViewModel genericVM)
		{
			UpdateGenericPreview(vm, genericVM);
			genericVM.PropertyChanged += (_, e) =>
			{
				if (e.PropertyName is nameof(GenericVisualizerNodeViewModel.PreviewImage)
					or nameof(GenericVisualizerNodeViewModel.DisplayText)
					or nameof(NodeViewModel.ShowPreview))
					UpdateGenericPreview(vm, genericVM);
			};
		}
		else if (vm is CSharpNodeViewModel csharpVM)
		{
			UpdateImagePreview(ImagePreview, vm, csharpVM.PreviewImage);
			csharpVM.PropertyChanged += (_, e) =>
			{
				if (e.PropertyName is nameof(CSharpNodeViewModel.PreviewImage) or nameof(NodeViewModel.ShowPreview))
					UpdateImagePreview(ImagePreview, vm, csharpVM.PreviewImage);
			};
		}
	}

	/// <summary>
	/// Updates the selection visual state.
	/// </summary>
	/// <param name="selected"><c>true</c> when the node is selected.</param>
	public void SetSelected(bool selected)
	{
		_isSelected = selected;
		UpdateNodeBorder();
	}

	private void UpdateNodeBorder()
	{
		NodeBorder.BorderBrush = new SolidColorBrush(
			_viewModel?.HasExecutionError == true
				? Windows.UI.Color.FromArgb(255, 229, 57, 53)
				: _isSelected
				? Windows.UI.Color.FromArgb(255, 100, 180, 255)
				: Windows.UI.Color.FromArgb(255, 85, 85, 85));
		NodeBorder.BorderThickness = new Thickness(_isSelected || _viewModel?.HasExecutionError == true ? 2 : 1);
	}

	private void ApplyNodeSize()
	{
		if (_viewModel is null)
			return;

		Width = _viewModel.Width;
		Height = _viewModel.Height;
		NodeBorder.Width = _viewModel.Width;
		NodeBorder.Height = _viewModel.Height;
	}

	private void UpdateResizeGrip()
	{
		ResizeGrip.Visibility = _viewModel?.CanResize == true
			? Visibility.Visible
			: Visibility.Collapsed;
	}

	private static void UpdateImagePreview(CvbImageDisplay previewControl, NodeViewModel vm, CommonVisionNodes.Contracts.ImagePreviewDto? preview)
	{
		previewControl.Visibility = vm.ShowPreview ? Visibility.Visible : Visibility.Collapsed;
		if (vm.ShowPreview)
			previewControl.SetImage(preview);
		else
			previewControl.Clear();
	}

	private void UpdateCropPreview(NodeViewModel vm, SubImageNodeViewModel subVM)
	{
		CropPreview.Visibility = vm.ShowPreview ? Visibility.Visible : Visibility.Collapsed;
		CropPreview.SetImage(vm.ShowPreview ? subVM.PreviewImage : null);
		CropPreview.UpdateCropOverlay(subVM.AreaX, subVM.AreaY, subVM.AreaWidth, subVM.AreaHeight);
	}

	private void UpdateBlobPreview(NodeViewModel vm, BlobNodeViewModel blobVM)
	{
		BlobPreview.Visibility = vm.ShowPreview ? Visibility.Visible : Visibility.Collapsed;
		BlobPreview.SetImage(vm.ShowPreview ? blobVM.PreviewImage : null);
		BlobPreview.SetBlobs(blobVM.Blobs);
	}

	private void UpdateClassificationPreview(
		NodeViewModel vm,
		ImagePreviewDto? previewImage,
		IReadOnlyList<ClassificationResultDto> results)
	{
		PolimagoPreview.Visibility = vm.ShowPreview ? Visibility.Visible : Visibility.Collapsed;
		PolimagoPreview.SetPreview(vm.ShowPreview ? previewImage : null, results);
	}

	private void UpdateCodeReaderPreview(NodeViewModel vm, CodeReaderNodeViewModel codeReaderVM)
	{
		CodeReaderPreview.Visibility = vm.ShowPreview ? Visibility.Visible : Visibility.Collapsed;
		UpdateCodeReaderPreviewImage(vm, codeReaderVM);
		UpdateCodeReaderPreviewResults(vm, codeReaderVM);
	}

	private void UpdateCodeReaderPreviewImage(NodeViewModel vm, CodeReaderNodeViewModel codeReaderVM)
	{
		CodeReaderPreview.Visibility = vm.ShowPreview ? Visibility.Visible : Visibility.Collapsed;
		CodeReaderPreview.SetImage(vm.ShowPreview ? codeReaderVM.PreviewImage : null);
	}

	private void UpdateCodeReaderPreviewResults(NodeViewModel vm, CodeReaderNodeViewModel codeReaderVM)
	{
		CodeReaderPreview.Visibility = vm.ShowPreview ? Visibility.Visible : Visibility.Collapsed;
		CodeReaderPreview.SetResults(codeReaderVM.Results, codeReaderVM.TimeLimitReached);
	}

	private void UpdateGenericPreview(NodeViewModel vm, GenericVisualizerNodeViewModel genericVM)
	{
		GenericVisualizerPreview.Visibility = vm.ShowPreview ? Visibility.Visible : Visibility.Collapsed;
		if (!vm.ShowPreview)
		{
			GenericVisualizerPreview.SetImagePreview(null);
			GenericVisualizerPreview.SetText(null);
			return;
		}

		if (genericVM.PreviewImage is not null)
			GenericVisualizerPreview.SetImagePreview(genericVM.PreviewImage);
		else
			GenericVisualizerPreview.SetText(genericVM.DisplayText);
	}

	private void UpdateSummary()
	{
		var summary = _viewModel?.Summary;
		if (!string.IsNullOrEmpty(summary))
		{
			SummaryText.Text = summary;
			SummaryText.Visibility = Visibility.Visible;
		}
		else
		{
			SummaryText.Visibility = Visibility.Collapsed;
		}
	}

	private void UpdateExecutionMetrics()
	{
		var time = _viewModel?.ExecutionTime;
		var fps = _viewModel?.SinkFps;
		if (!string.IsNullOrEmpty(time) || !string.IsNullOrEmpty(fps))
		{
			ExecutionTimeText.Text = time;
			SinkFpsText.Text = fps;
			ExecutionMetricsPanel.Visibility = Visibility.Visible;
		}
		else
		{
			ExecutionMetricsPanel.Visibility = Visibility.Collapsed;
		}
	}

	private void UpdateExecutionError()
	{
		var error = _viewModel?.ExecutionErrorText;
		if (!string.IsNullOrWhiteSpace(error))
		{
			ExecutionErrorText.Text = error;
			ExecutionErrorPanel.Visibility = Visibility.Visible;
			ToolTipService.SetToolTip(ExecutionErrorPanel, error);
		}
		else
		{
			ExecutionErrorText.Text = string.Empty;
			ExecutionErrorPanel.Visibility = Visibility.Collapsed;
			ToolTipService.SetToolTip(ExecutionErrorPanel, null);
		}
	}

	private void Header_PointerPressed(object sender, PointerRoutedEventArgs e)
	{
		if (_viewModel == null) return;
		if (_isResizing) return;
		_isDragging = true;
		_hasMoved = false;
		if (Parent is not UIElement canvas) return;
		_dragStart = e.GetCurrentPoint(canvas).Position;
		_startX = _viewModel.X;
		_startY = _viewModel.Y;
		((UIElement)sender).CapturePointer(e.Pointer);
		e.Handled = true;
	}

	private void Header_PointerMoved(object sender, PointerRoutedEventArgs e)
	{
		if (!_isDragging || _viewModel == null) return;
		if (Parent is not UIElement canvas) return;
		var current = e.GetCurrentPoint(canvas).Position;
		_viewModel.X = _startX + (current.X - _dragStart.X);
		_viewModel.Y = _startY + (current.Y - _dragStart.Y);
		Canvas.SetLeft(this, _viewModel.X);
		Canvas.SetTop(this, _viewModel.Y);
		_hasMoved = true;
		NodeMoved?.Invoke(this);
		e.Handled = true;
	}

	private void Header_PointerReleased(object sender, PointerRoutedEventArgs e)
	{
		if (!_isDragging) return;
		_isDragging = false;
		((UIElement)sender).ReleasePointerCapture(e.Pointer);

		if (!_hasMoved)
			NodeSelected?.Invoke(this);

		e.Handled = true;
	}

	private void ResizeGrip_PointerPressed(object sender, PointerRoutedEventArgs e)
	{
		if (_viewModel?.CanResize != true) return;
		if (Parent is not UIElement canvas) return;

		_isDragging = false;
		_isResizing = true;
		_resizeStart = e.GetCurrentPoint(canvas).Position;
		_startWidth = _viewModel.Width;
		_startHeight = _viewModel.Height;
		((UIElement)sender).CapturePointer(e.Pointer);
		e.Handled = true;
	}

	private void ResizeGrip_PointerMoved(object sender, PointerRoutedEventArgs e)
	{
		if (!_isResizing || _viewModel == null) return;
		if (Parent is not UIElement canvas) return;

		var current = e.GetCurrentPoint(canvas).Position;
		_viewModel.Width = Math.Max(NodeViewModel.MinNodeWidth, _startWidth + current.X - _resizeStart.X);
		_viewModel.Height = Math.Max(_viewModel.MinimumContentHeight, _startHeight + current.Y - _resizeStart.Y);
		e.Handled = true;
	}

	private void ResizeGrip_PointerReleased(object sender, PointerRoutedEventArgs e)
	{
		if (!_isResizing) return;

		_isResizing = false;
		((UIElement)sender).ReleasePointerCapture(e.Pointer);
		e.Handled = true;
	}

	private void Port_PointerPressed(object sender, PointerRoutedEventArgs e)
	{
		if (_viewModel?.IsGraphRunning == true)
		{
			e.Handled = true;
			return;
		}

		if (sender is Ellipse ellipse && ellipse.DataContext is PortViewModel port)
		{
			if (e.GetCurrentPoint(null).Properties.IsRightButtonPressed)
			{
				PortRightTapped?.Invoke(this, port);
				e.Handled = true;
				return;
			}

			IsConnectionDragging = true;

			if (ellipse.Parent is FrameworkElement parent && ToolTipService.GetToolTip(parent) is ToolTip tooltip)
				tooltip.IsOpen = false;

			PortPressed?.Invoke(this, port, e);
			e.Handled = true;
		}
	}

	private void PortToolTip_Opened(object sender, RoutedEventArgs e)
	{
		if (IsConnectionDragging && sender is ToolTip tooltip)
			tooltip.IsOpen = false;
	}
}
