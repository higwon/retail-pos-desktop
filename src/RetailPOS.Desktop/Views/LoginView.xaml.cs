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
        viewModel.SignedIn += OnSignedIn;
    }

    public event RoutedEventHandler? ContinueRequested;

    private void OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is LoginViewModel viewModel && sender is PasswordBox passwordBox)
        {
            viewModel.Password = passwordBox.Password;
        }
    }

    private void OnSignedIn(object? sender, EventArgs e) => ContinueRequested?.Invoke(this, new RoutedEventArgs());
}
