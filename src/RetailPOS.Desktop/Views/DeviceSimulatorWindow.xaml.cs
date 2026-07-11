using System.Windows;
using RetailPOS.Desktop.ViewModels;

namespace RetailPOS.Desktop.Views;

public partial class DeviceSimulatorWindow : Window
{
    public DeviceSimulatorWindow(DeviceSimulatorViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += OnLoaded;
        Closed += OnClosed;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        if (DataContext is DeviceSimulatorViewModel viewModel)
        {
            await viewModel.BarcodeScanner.LoadAsync();
        }
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();

    private void OnClosed(object? sender, EventArgs e)
    {
        Closed -= OnClosed;
        (DataContext as IDisposable)?.Dispose();
        DataContext = null;
    }
}
