using System.Windows.Controls;
using RetailPOS.Desktop.ViewModels;

namespace RetailPOS.Desktop.Views;

public partial class CheckoutRecoveryView : UserControl
{
    public CheckoutRecoveryView(CheckoutRecoveryViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
