using System.Windows;
using RetailPOS.Desktop.ViewModels;
using RetailPOS.Desktop.Workflow;

namespace RetailPOS.Desktop.Views;

public partial class PaymentDialog : Window, IWorkflowWindow
{
    public PaymentDialog(PaymentDialogViewModel viewModel)
    {
        InitializeComponent();
        Owner = System.Windows.Application.Current?.MainWindow;
        DataContext = viewModel;
        Closed += OnClosed;
    }

    void IWorkflowWindow.Activate() => Activate();

    private void OnClose(object sender, RoutedEventArgs e) => Close();

    private void OnClosed(object? sender, EventArgs e)
    {
        Closed -= OnClosed;
        (DataContext as IDisposable)?.Dispose();
        DataContext = null;
    }
}
