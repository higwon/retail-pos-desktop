using System.Windows.Controls;
using RetailPOS.Desktop.Views;

namespace RetailPOS.Desktop.Controls;

public partial class NavigationHost : UserControl
{
    private readonly LoginView _loginView;
    private readonly PosMainView _posMainView;
    private readonly CheckoutRecoveryView _checkoutRecoveryView;
    private readonly DashboardView _dashboardView;
    private readonly StatusView _statusView;

    public NavigationHost(LoginView loginView, PosMainView posMainView,
        CheckoutRecoveryView checkoutRecoveryView, DashboardView dashboardView, StatusView statusView)
    {
        InitializeComponent();
        _loginView = loginView;
        _posMainView = posMainView;
        _checkoutRecoveryView = checkoutRecoveryView;
        _dashboardView = dashboardView;
        _statusView = statusView;
        _loginView.ContinueRequested += ShowPosMain;
        ContentRoot.Children.Add(_loginView);
    }

    private void ShowPosMain(object? sender, System.Windows.RoutedEventArgs e)
    {
        DemoNavigation.Visibility = System.Windows.Visibility.Visible;
        Grid.SetRow(ContentRoot, 1);
        Grid.SetRowSpan(ContentRoot, 1);
        Show(_posMainView);
    }

    private void Show(UserControl view)
    {
        ContentRoot.Children.Clear();
        ContentRoot.Children.Add(view);
    }

    private void OnShowRegister(object sender, System.Windows.RoutedEventArgs e) => Show(_posMainView);
    private void OnShowRecovery(object sender, System.Windows.RoutedEventArgs e) => Show(_checkoutRecoveryView);
    private void OnShowDashboard(object sender, System.Windows.RoutedEventArgs e) => Show(_dashboardView);
    private void OnShowStatus(object sender, System.Windows.RoutedEventArgs e) => Show(_statusView);
}
