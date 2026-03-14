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
            ImagePreview.Visibility = Visibility.Visible;
            imageVM.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(ImageNodeViewModel.PreviewImage))
                    ImagePreview.SetImage(imageVM.PreviewImage);
            };
        }
        else if (vm is SaveImageNodeViewModel saveVM)
        {
            ImagePreview.Visibility = Visibility.Visible;
            saveVM.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(SaveImageNodeViewModel.PreviewImage))
                    ImagePreview.SetImage(saveVM.PreviewImage);
            };
        }
        else if (vm is BinarizeNodeViewModel binarizeVM)
        {
            ImagePreview.Visibility = Visibility.Visible;
            binarizeVM.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(BinarizeNodeViewModel.PreviewImage))
                    ImagePreview.SetImage(binarizeVM.PreviewImage);
            };
        }
        else if (vm is SubImageNodeViewModel subVM)
        {
            CropPreview.Visibility = Visibility.Visible;
            subVM.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(SubImageNodeViewModel.PreviewImage))
                {
                    CropPreview.SetImage(subVM.PreviewImage);
                    CropPreview.UpdateCropOverlay(subVM.AreaX, subVM.AreaY, subVM.AreaWidth, subVM.AreaHeight);
                }
                else if (e.PropertyName is nameof(SubImageNodeViewModel.AreaX)
                                         or nameof(SubImageNodeViewModel.AreaY)
                                         or nameof(SubImageNodeViewModel.AreaWidth)
                                         or nameof(SubImageNodeViewModel.AreaHeight))
                {
                    CropPreview.UpdateCropOverlay(subVM.AreaX, subVM.AreaY, subVM.AreaWidth, subVM.AreaHeight);
                }
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
            ImagePreview.Visibility = Visibility.Visible;
            transformVM.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(MatrixTransformNodeViewModel.PreviewImage))
                    ImagePreview.SetImage(transformVM.PreviewImage);
            };
        }
        else if (vm is ImageGeneratorNodeViewModel genVM)
        {
            ImagePreview.Visibility = Visibility.Visible;
            genVM.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(ImageGeneratorNodeViewModel.PreviewImage))
                    ImagePreview.SetImage(genVM.PreviewImage);
            };
        }
        else if (vm is FilterNodeViewModel filterVM)
        {
            ImagePreview.Visibility = Visibility.Visible;
            filterVM.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(FilterNodeViewModel.PreviewImage))
                    ImagePreview.SetImage(filterVM.PreviewImage);
            };
        }
        else if (vm is MorphologyNodeViewModel morphVM)
        {
            ImagePreview.Visibility = Visibility.Visible;
            morphVM.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(MorphologyNodeViewModel.PreviewImage))
                    ImagePreview.SetImage(morphVM.PreviewImage);
            };
        }
        else if (vm is NormalizeNodeViewModel normalizeVM)
        {
            ImagePreview.Visibility = Visibility.Visible;
            normalizeVM.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(NormalizeNodeViewModel.PreviewImage))
                    ImagePreview.SetImage(normalizeVM.PreviewImage);
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
            BlobPreview.Visibility = Visibility.Visible;
            blobVM.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(BlobNodeViewModel.PreviewImage))
                    BlobPreview.SetImage(blobVM.PreviewImage);
                else if (e.PropertyName == nameof(BlobNodeViewModel.Blobs))
                    BlobPreview.SetBlobs(blobVM.Blobs);
            };
        }
        else if (vm is PolimagoClassifyNodeViewModel polimagoVM)
        {
            PolimagoPreview.Visibility = Visibility.Visible;
            polimagoVM.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(PolimagoClassifyNodeViewModel.PreviewImage))
                    PolimagoPreview.SetImage(polimagoVM.PreviewImage);
                else if (e.PropertyName == nameof(PolimagoClassifyNodeViewModel.Results))
                    PolimagoPreview.SetResults(polimagoVM.Results);
            };
        }
        else if (vm is GenericVisualizerNodeViewModel genericVM)
        {
            GenericVisualizerPreview.Visibility = Visibility.Visible;
            genericVM.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(GenericVisualizerNodeViewModel.PreviewImage))
                    GenericVisualizerPreview.SetImagePreview(genericVM.PreviewImage);
                else if (e.PropertyName == nameof(GenericVisualizerNodeViewModel.DisplayText))
                    GenericVisualizerPreview.SetText(genericVM.DisplayText);
            };
        }
        else if (vm is CSharpNodeViewModel csharpVM)
        {
            ImagePreview.Visibility = Visibility.Visible;
            csharpVM.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(CSharpNodeViewModel.PreviewImage))
                    ImagePreview.SetImage(csharpVM.PreviewImage);
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
