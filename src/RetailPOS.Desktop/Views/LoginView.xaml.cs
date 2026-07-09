using RetailPOS.Desktop.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace RetailPOS.Desktop.Views;

public partial class LoginView : UserControl
{
    private readonly LoginViewModel _viewModel;
    private bool _isSignedInSubscribed;

    public LoginView(LoginViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
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

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_isSignedInSubscribed)
        {
            return;
        }

        _viewModel.SignedIn += OnSignedIn;
        _isSignedInSubscribed = true;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (!_isSignedInSubscribed)
        {
            return;
        }

        _viewModel.SignedIn -= OnSignedIn;
        _isSignedInSubscribed = false;
    }
}
