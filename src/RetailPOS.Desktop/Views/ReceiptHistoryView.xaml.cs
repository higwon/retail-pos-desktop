using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using RetailPOS.Desktop.ViewModels;

namespace RetailPOS.Desktop.Views;

public partial class ReceiptHistoryView : UserControl
{
    private readonly ReceiptHistoryViewModel _viewModel;

    public ReceiptHistoryView(ReceiptHistoryViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await _viewModel.ActivateAsync();
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e) => _viewModel.Deactivate();

    private void OnDatePickerPreviewMouseLeftButtonDown(
        object sender,
        MouseButtonEventArgs e)
    {
        if (sender is DatePicker { IsDropDownOpen: false } datePicker)
        {
            datePicker.IsDropDownOpen = true;
            e.Handled = true;
        }
    }
}
