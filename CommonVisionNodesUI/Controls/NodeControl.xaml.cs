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

    public event Action<NodeControl>? NodeMoved;
    public event Action<NodeControl, PortViewModel, PointerRoutedEventArgs>? PortPressed;
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

    private void Header_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (_viewModel == null) return;
        _isDragging = true;
        _hasMoved = false;
        var canvas = this.Parent as UIElement;
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
        var canvas = this.Parent as UIElement;
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
            PortPressed?.Invoke(this, port, e);
            e.Handled = true;
        }
    }
}
