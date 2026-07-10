using RetailPOS.Desktop.ViewModels;
using System.Windows;
using RetailPOS.Desktop.DeviceSimulation;

namespace RetailPOS.Desktop.Views;

public partial class CustomerDisplayWindow : Window, ICustomerDisplayWindow
{
    public CustomerDisplayWindow(CustomerDisplayViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Closed += OnClosed;
    }

    public void ShowOn(DisplayTarget target)
    {
        WindowStartupLocation = WindowStartupLocation.Manual;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        Left = target.Bounds.Left;
        Top = target.Bounds.Top;
        Show();
        WindowState = WindowState.Maximized;
    }

    void ICustomerDisplayWindow.Activate() => Activate();

    private void OnClosed(object? sender, EventArgs e)
    {
        Closed -= OnClosed;
        (DataContext as IDisposable)?.Dispose();
        DataContext = null;
    }
}
