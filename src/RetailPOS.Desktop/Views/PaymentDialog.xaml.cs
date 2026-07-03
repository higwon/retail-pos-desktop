using System.Windows;
using RetailPOS.Desktop.ViewModels;

namespace RetailPOS.Desktop.Views;

public partial class PaymentDialog : Window
{
    public PaymentDialog(PaymentDialogViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();
}
