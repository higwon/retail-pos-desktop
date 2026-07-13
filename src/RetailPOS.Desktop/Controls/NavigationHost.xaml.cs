using System.Windows.Controls;
using Microsoft.Extensions.Logging;
using RetailPOS.Application.Checkout;
using RetailPOS.Desktop.DeviceSimulation;
using RetailPOS.Desktop.Views;
using RetailPOS.Desktop.Workflow;

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
    private readonly SessionSignOutCoordinator _signOutCoordinator;
    private readonly CashierWorkflowNavigator _workflowNavigator;
    private bool _recoveryCheckedAfterSignIn;
    private bool _isLoginSubscribed;
    private bool _isWorkflowSubscribed;

    public NavigationHost(LoginView loginView, PosMainView posMainView,
        CheckoutRecoveryView checkoutRecoveryView, DashboardView dashboardView, StatusView statusView,
        ICheckoutRecoveryService checkoutRecoveryService,
        DeviceSimulatorWindowHost deviceSimulatorWindowHost,
        SessionSignOutCoordinator signOutCoordinator,
        CashierWorkflowNavigator workflowNavigator,
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
        _signOutCoordinator = signOutCoordinator;
        _workflowNavigator = workflowNavigator;
        DeviceSimulatorButton.Visibility = deviceSimulatorWindowHost.IsEnabled
            ? System.Windows.Visibility.Visible
            : System.Windows.Visibility.Collapsed;
        ShowScreen(workflowNavigator.Current);
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        SubscribeLogin();
        SubscribeWorkflow();
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

        _workflowNavigator.Navigate(CashierWorkflowScreen.Recovery);
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
        if (_isLoginSubscribed)
        {
            _loginView.ContinueRequested -= ShowPosMain;
            _isLoginSubscribed = false;
        }

        if (_isWorkflowSubscribed)
        {
            _workflowNavigator.ScreenChanged -= OnWorkflowScreenChanged;
            _isWorkflowSubscribed = false;
        }
    }

    private async void ShowPosMain(object? sender, System.Windows.RoutedEventArgs e)
    {
        DemoNavigation.Visibility = System.Windows.Visibility.Visible;
        Grid.SetRow(ContentRoot, 1);
        Grid.SetRowSpan(ContentRoot, 1);
        _workflowNavigator.Reset(CashierWorkflowScreen.Register);
        await ShowRecoveryAfterSignInAsync();
    }

    private void SubscribeWorkflow()
    {
        if (_isWorkflowSubscribed)
        {
            return;
        }

        _workflowNavigator.ScreenChanged += OnWorkflowScreenChanged;
        _isWorkflowSubscribed = true;
        ShowScreen(_workflowNavigator.Current);
    }

    private void OnWorkflowScreenChanged(
        object? sender,
        CashierWorkflowChangedEventArgs e)
    {
        if (!Dispatcher.CheckAccess())
        {
            _ = Dispatcher.BeginInvoke(() => ShowScreen(_workflowNavigator.Current));
            return;
        }

        ShowScreen(e.Current);
    }

    private void ShowScreen(CashierWorkflowScreen screen)
    {
        UserControl view = screen switch
        {
            CashierWorkflowScreen.Login => _loginView,
            CashierWorkflowScreen.Register => _posMainView,
            CashierWorkflowScreen.Recovery => _checkoutRecoveryView,
            CashierWorkflowScreen.Dashboard => _dashboardView,
            CashierWorkflowScreen.Status => _statusView,
            _ => throw new InvalidOperationException(
                $"Cashier workflow screen {screen} does not have a registered view yet.")
        };

        ContentRoot.Children.Clear();
        ContentRoot.Children.Add(view);
    }

    private void OnShowRegister(object sender, System.Windows.RoutedEventArgs e) =>
        _workflowNavigator.Reset(CashierWorkflowScreen.Register);
    private void OnShowRecovery(object sender, System.Windows.RoutedEventArgs e) =>
        _workflowNavigator.Reset(CashierWorkflowScreen.Recovery);
    private void OnShowDashboard(object sender, System.Windows.RoutedEventArgs e) =>
        _workflowNavigator.Reset(CashierWorkflowScreen.Dashboard);
    private void OnShowStatus(object sender, System.Windows.RoutedEventArgs e) =>
        _workflowNavigator.Reset(CashierWorkflowScreen.Status);
    private void OnOpenDeviceSimulator(object sender, System.Windows.RoutedEventArgs e) =>
        _deviceSimulatorWindowHost.ShowOrActivate();

    private void OnSignOut(object sender, System.Windows.RoutedEventArgs e)
    {
        _signOutCoordinator.SignOut();
        _recoveryCheckedAfterSignIn = false;
        DemoNavigation.Visibility = System.Windows.Visibility.Collapsed;
        Grid.SetRow(ContentRoot, 0);
        Grid.SetRowSpan(ContentRoot, 2);
    }
}
