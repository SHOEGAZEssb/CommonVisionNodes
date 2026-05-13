using CommonVisionNodesUI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;

namespace CommonVisionNodesUI.Controls;

public sealed partial class NodeControl : UserControl
{
    private NodeViewModel? _viewModel;
    private bool _isDragging;
    private bool _hasMoved;
    private Point _dragStart;
    private double _startX;
    private double _startY;

    internal static bool IsConnectionDragging;

    public event Action<NodeControl>? NodeMoved;
    public event Action<NodeControl, PortViewModel, PointerRoutedEventArgs>? PortPressed;
    public event Action<NodeControl, PortViewModel>? PortRightTapped;
    public event Action<NodeControl>? NodeSelected;

    public NodeViewModel? ViewModel => _viewModel;

    public NodeControl()
    {
        this.InitializeComponent();
    }

    public void SetViewModel(NodeViewModel vm)
    {
        _viewModel = vm;
        TitleText.Text = vm.Title;
        HeaderBorder.Background = new SolidColorBrush(vm.HeaderColor);
        InputPortsList.ItemsSource = vm.InputPorts;
        OutputPortsList.ItemsSource = vm.OutputPorts;

        Canvas.SetLeft(this, vm.X);
        Canvas.SetTop(this, vm.Y);

        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(NodeViewModel.Summary))
                UpdateSummary();
            else if (e.PropertyName == nameof(NodeViewModel.ExecutionTime))
                UpdateExecutionTime();
        };
        UpdateSummary();

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
        else if (vm is PolimagoClassifyNodeViewModel polimagoVM)
        {
            UpdatePolimagoPreview(vm, polimagoVM);
            polimagoVM.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName is nameof(PolimagoClassifyNodeViewModel.PreviewImage)
                    or nameof(PolimagoClassifyNodeViewModel.Results)
                    or nameof(NodeViewModel.ShowPreview))
                    UpdatePolimagoPreview(vm, polimagoVM);
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

    public void SetSelected(bool selected)
    {
        NodeBorder.BorderBrush = new SolidColorBrush(
            selected
                ? Windows.UI.Color.FromArgb(255, 100, 180, 255)
                : Windows.UI.Color.FromArgb(255, 85, 85, 85));
        NodeBorder.BorderThickness = new Thickness(selected ? 2 : 1);
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

    private void UpdatePolimagoPreview(NodeViewModel vm, PolimagoClassifyNodeViewModel polimagoVM)
    {
        PolimagoPreview.Visibility = vm.ShowPreview ? Visibility.Visible : Visibility.Collapsed;
        PolimagoPreview.SetImage(vm.ShowPreview ? polimagoVM.PreviewImage : null);
        PolimagoPreview.SetResults(polimagoVM.Results);
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

    private void UpdateExecutionTime()
    {
        var time = _viewModel?.ExecutionTime;
        if (!string.IsNullOrEmpty(time))
        {
            ExecutionTimeText.Text = time;
            ExecutionTimeText.Visibility = Visibility.Visible;
        }
        else
        {
            ExecutionTimeText.Visibility = Visibility.Collapsed;
        }
    }

    private void Header_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (_viewModel == null) return;
        _isDragging = true;
        _hasMoved = false;
        var canvas = Parent as UIElement;
        if (canvas == null) return;
        _dragStart = e.GetCurrentPoint(canvas).Position;
        _startX = _viewModel.X;
        _startY = _viewModel.Y;
        ((UIElement)sender).CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private void Header_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_isDragging || _viewModel == null) return;
        var canvas = Parent as UIElement;
        if (canvas == null) return;
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

    private void Port_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
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
