using RetailPOS.Desktop.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace RetailPOS.Desktop.Views;

public partial class LoginView : UserControl
{
    public LoginView(LoginViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    public event RoutedEventHandler? ContinueRequested;

    private void OnContinueClick(object sender, RoutedEventArgs e) => ContinueRequested?.Invoke(this, e);
}
