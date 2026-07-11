using System.Windows;
using RetailPOS.Desktop.ViewModels;
using RetailPOS.Desktop.Workflow;

namespace RetailPOS.Desktop.Views;

public partial class PaymentDialog : Window, IWorkflowWindow
{
    private readonly Window? _ownerWindow;

    public PaymentDialog(PaymentDialogViewModel viewModel)
    {
        InitializeComponent();
        _ownerWindow = System.Windows.Application.Current?.MainWindow;
        Owner = _ownerWindow;
        if (_ownerWindow is not null)
        {
            _ownerWindow.IsEnabled = false;
        }
        DataContext = viewModel;
        Closed += OnClosed;
    }

    void IWorkflowWindow.Activate() => Activate();

    private void OnClose(object sender, RoutedEventArgs e) => Close();

    private void OnClosed(object? sender, EventArgs e)
    {
        Closed -= OnClosed;
        if (_ownerWindow is not null)
        {
            _ownerWindow.IsEnabled = true;
        }
        (DataContext as IDisposable)?.Dispose();
        DataContext = null;
    }
}
