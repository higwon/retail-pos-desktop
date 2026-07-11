using System.Windows.Controls;
using Microsoft.Extensions.Logging;
using RetailPOS.Application.Checkout;
using RetailPOS.Desktop.DeviceSimulation;
using RetailPOS.Desktop.Views;

namespace RetailPOS.Desktop.Controls;

public partial class NavigationHost : UserControl
{
    private readonly ICheckoutRecoveryService _checkoutRecoveryService;
    private readonly ILogger<NavigationHost> _logger;
    private readonly LoginView _loginView;
    private readonly PosMainView _posMainView;
    private readonly CheckoutRecoveryView _checkoutRecoveryView;
    private readonly DashboardView _dashboardView;
    private readonly StatusView _statusView;
    private readonly DeviceSimulatorWindowHost _deviceSimulatorWindowHost;
    private bool _recoveryCheckedAfterSignIn;
    private bool _isLoginSubscribed;

    public NavigationHost(LoginView loginView, PosMainView posMainView,
        CheckoutRecoveryView checkoutRecoveryView, DashboardView dashboardView, StatusView statusView,
        ICheckoutRecoveryService checkoutRecoveryService,
        DeviceSimulatorWindowHost deviceSimulatorWindowHost,
        ILogger<NavigationHost> logger)
    {
        InitializeComponent();
        _checkoutRecoveryService = checkoutRecoveryService;
        _logger = logger;
        _loginView = loginView;
        _posMainView = posMainView;
        _checkoutRecoveryView = checkoutRecoveryView;
        _dashboardView = dashboardView;
        _statusView = statusView;
        _deviceSimulatorWindowHost = deviceSimulatorWindowHost;
        DeviceSimulatorButton.Visibility = deviceSimulatorWindowHost.IsEnabled
            ? System.Windows.Visibility.Visible
            : System.Windows.Visibility.Collapsed;
        ContentRoot.Children.Add(_loginView);
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        SubscribeLogin();
    }

    private async Task ShowRecoveryAfterSignInAsync()
    {
        if (_recoveryCheckedAfterSignIn) return;
        _recoveryCheckedAfterSignIn = true;
        IReadOnlyList<CheckoutRecoveryRecord> recoverable;
        try
        {
            recoverable = await _checkoutRecoveryService.GetRecoverableAsync();
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Checkout recovery startup detection failed.");
            return;
        }

        if (recoverable.Count == 0)
        {
            return;
        }

        Show(_checkoutRecoveryView);
    }

    private void SubscribeLogin()
    {
        if (_isLoginSubscribed)
        {
            return;
        }

        _loginView.ContinueRequested += ShowPosMain;
        _isLoginSubscribed = true;
    }

    private void OnUnloaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (!_isLoginSubscribed)
        {
            return;
        }

        _loginView.ContinueRequested -= ShowPosMain;
        _isLoginSubscribed = false;
    }

    private async void ShowPosMain(object? sender, System.Windows.RoutedEventArgs e)
    {
        DemoNavigation.Visibility = System.Windows.Visibility.Visible;
        Grid.SetRow(ContentRoot, 1);
        Grid.SetRowSpan(ContentRoot, 1);
        Show(_posMainView);
        await ShowRecoveryAfterSignInAsync();
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
    private void OnOpenDeviceSimulator(object sender, System.Windows.RoutedEventArgs e) =>
        _deviceSimulatorWindowHost.ShowOrActivate();
}
