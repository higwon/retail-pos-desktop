using System.Windows.Controls;
using RetailPOS.Desktop.Views;

namespace RetailPOS.Desktop.Controls;

public partial class NavigationHost : UserControl
{
    private readonly LoginView _loginView;
    private readonly PosMainView _posMainView;

    public NavigationHost(LoginView loginView, PosMainView posMainView)
    {
        InitializeComponent();
        _loginView = loginView;
        _posMainView = posMainView;
        _loginView.ContinueRequested += ShowPosMain;
        ContentRoot.Children.Add(_loginView);
    }

    private void ShowPosMain(object? sender, System.Windows.RoutedEventArgs e)
    {
        ContentRoot.Children.Clear();
        ContentRoot.Children.Add(_posMainView);
    }
}
