using RetailPOS.Desktop.ViewModels;
using System.Windows;

namespace RetailPOS.Desktop.Views;

public partial class CustomerDisplayWindow : Window
{
    public CustomerDisplayWindow(CustomerDisplayViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
