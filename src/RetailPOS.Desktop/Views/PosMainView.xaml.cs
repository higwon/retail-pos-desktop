using RetailPOS.Desktop.ViewModels;
using RetailPOS.Desktop.DeviceSimulation;
using System.Windows.Controls;
using RetailPOS.Desktop.Workflow;

namespace RetailPOS.Desktop.Views;

public partial class PosMainView : UserControl
{
    private readonly ReceiptPreviewState _receiptPreviewState;
    private readonly CustomerDisplayHost _customerDisplayHost;
    private readonly WorkflowWindowHost<PaymentDialog> _paymentDialogHost;
    private readonly WorkflowWindowHost<ReceiptDialog> _receiptDialogHost;
    private readonly PosMainViewModel _viewModel;
    private readonly CartPanelView _cartPanel;
    private readonly BarcodeScannerCoordinator _barcodeScannerCoordinator;
    private bool _loadedOnce;
    private bool _isCheckoutSubscribed;
    private bool _isPaymentHostSubscribed;

    public PosMainView(
        PosMainViewModel viewModel,
        ProductGridView productGrid,
        CartPanelView cartPanel,
        BarcodeScannerCoordinator barcodeScannerCoordinator,
        ReceiptPreviewState receiptPreviewState,
        CustomerDisplayHost customerDisplayHost,
        WorkflowWindowHost<PaymentDialog> paymentDialogHost,
        WorkflowWindowHost<ReceiptDialog> receiptDialogHost)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        _receiptPreviewState = receiptPreviewState;
        _customerDisplayHost = customerDisplayHost;
        _paymentDialogHost = paymentDialogHost;
        _receiptDialogHost = receiptDialogHost;
        _cartPanel = cartPanel;
        _barcodeScannerCoordinator = barcodeScannerCoordinator;
        ProductRegion.Content = productGrid;
        CartRegion.Content = cartPanel;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private async void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        SubscribeCheckout();
        SubscribePaymentHost();
        _barcodeScannerCoordinator.Start();

        if (_loadedOnce)
        {
            return;
        }

        _loadedOnce = true;
        await _viewModel.LoadAsync();
    }

    private void OnOpenCustomerDisplay(object sender, System.Windows.RoutedEventArgs e)
    {
        OpenCustomerDisplay();
    }

    private void OpenCustomerDisplay()
    {
        _customerDisplayHost.RefreshTargets();
        var target = _customerDisplayHost.Targets.FirstOrDefault(item => !item.IsPrimary);
        if (target is not null) _customerDisplayHost.Open(target.Id);
    }

    private void OnCheckoutRequested(object? sender, EventArgs e) => OpenPaymentFlow();

    private void SubscribeCheckout()
    {
        if (_isCheckoutSubscribed)
        {
            return;
        }

        _cartPanel.CheckoutRequested += OnCheckoutRequested;
        _isCheckoutSubscribed = true;
    }

    private void OnUnloaded(object sender, System.Windows.RoutedEventArgs e)
    {
        _barcodeScannerCoordinator.Stop();
        if (_isPaymentHostSubscribed)
        {
            _paymentDialogHost.WindowClosed -= OnPaymentWindowClosed;
            _isPaymentHostSubscribed = false;
        }
        if (!_isCheckoutSubscribed)
        {
            return;
        }

        _cartPanel.CheckoutRequested -= OnCheckoutRequested;
        _isCheckoutSubscribed = false;
    }

    private void OpenPaymentFlow()
    {
        _paymentDialogHost.ShowOrActivate();
    }

    private void SubscribePaymentHost()
    {
        if (_isPaymentHostSubscribed) return;
        _paymentDialogHost.WindowClosed += OnPaymentWindowClosed;
        _isPaymentHostSubscribed = true;
    }

    private void OnPaymentWindowClosed(object? sender, EventArgs e)
    {
        if (_receiptPreviewState.HasReceipt)
        {
            _receiptDialogHost.ShowOrActivate();
        }
    }

    private void OnOpenReceipt(object sender, System.Windows.RoutedEventArgs e) =>
        _receiptDialogHost.ShowOrActivate();
}
