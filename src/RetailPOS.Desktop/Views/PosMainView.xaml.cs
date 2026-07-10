using RetailPOS.Desktop.ViewModels;
using RetailPOS.Desktop.DeviceSimulation;
using System.Windows.Controls;

namespace RetailPOS.Desktop.Views;

public partial class PosMainView : UserControl
{
    private readonly ReceiptPreviewState _receiptPreviewState;
    private readonly Func<CustomerDisplayWindow> _customerDisplayFactory;
    private readonly Func<PaymentDialog> _paymentDialogFactory;
    private readonly Func<ReceiptDialog> _receiptDialogFactory;
    private readonly PosMainViewModel _viewModel;
    private readonly CartPanelView _cartPanel;
    private readonly BarcodeScannerCoordinator _barcodeScannerCoordinator;
    private bool _loadedOnce;
    private bool _isCheckoutSubscribed;

    public PosMainView(
        PosMainViewModel viewModel,
        ProductGridView productGrid,
        CartPanelView cartPanel,
        BarcodeScannerCoordinator barcodeScannerCoordinator,
        ReceiptPreviewState receiptPreviewState,
        Func<CustomerDisplayWindow> customerDisplayFactory,
        Func<PaymentDialog> paymentDialogFactory,
        Func<ReceiptDialog> receiptDialogFactory)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        _receiptPreviewState = receiptPreviewState;
        _customerDisplayFactory = customerDisplayFactory;
        _paymentDialogFactory = paymentDialogFactory;
        _receiptDialogFactory = receiptDialogFactory;
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
        _customerDisplayFactory().Show();
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
        if (!_isCheckoutSubscribed)
        {
            return;
        }

        _cartPanel.CheckoutRequested -= OnCheckoutRequested;
        _isCheckoutSubscribed = false;
    }

    private void OpenPaymentFlow()
    {
        _paymentDialogFactory().ShowDialog();
        if (_receiptPreviewState.HasReceipt)
        {
            _receiptDialogFactory().ShowDialog();
        }
    }

    private void OnOpenReceipt(object sender, System.Windows.RoutedEventArgs e) =>
        _receiptDialogFactory().ShowDialog();
}
