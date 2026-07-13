using RetailPOS.Desktop.ViewModels;
using RetailPOS.Desktop.DeviceSimulation;
using System.Windows.Controls;
using RetailPOS.Desktop.Workflow;

namespace RetailPOS.Desktop.Views;

public partial class PosMainView : UserControl
{
    private readonly ReceiptPreviewState _receiptPreviewState;
    private readonly CustomerDisplayHost _customerDisplayHost;
    private readonly WorkflowWindowHost<ReceiptDialog> _receiptDialogHost;
    private readonly PosMainViewModel _viewModel;
    private readonly CartPanelView _cartPanel;
    private bool _arePaymentEventsSubscribed;
    private bool _loadedOnce;

    public PosMainView(
        PosMainViewModel viewModel,
        ProductGridViewModel productGrid,
        CartPanelView cartPanel,
        ReceiptPreviewState receiptPreviewState,
        CustomerDisplayHost customerDisplayHost,
        WorkflowWindowHost<ReceiptDialog> receiptDialogHost)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        _receiptPreviewState = receiptPreviewState;
        _customerDisplayHost = customerDisplayHost;
        _receiptDialogHost = receiptDialogHost;
        _cartPanel = cartPanel;
        ScannerStatusText.DataContext = productGrid;
        CartRegion.Content = cartPanel;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private async void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        SubscribePaymentEvents();
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

    private void OnCardPaymentCompleted(object? sender, EventArgs e) => OpenReceiptAfterPayment();

    private void OnCashPaymentCompleted(object? sender, EventArgs e)
    {
        OpenReceiptAfterPayment();
    }

    private void SubscribePaymentEvents()
    {
        if (_arePaymentEventsSubscribed)
        {
            return;
        }

        _cartPanel.CardPaymentCompleted += OnCardPaymentCompleted;
        _cartPanel.CashPaymentCompleted += OnCashPaymentCompleted;
        _arePaymentEventsSubscribed = true;
    }

    private void OnUnloaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (!_arePaymentEventsSubscribed)
        {
            return;
        }

        _cartPanel.CardPaymentCompleted -= OnCardPaymentCompleted;
        _cartPanel.CashPaymentCompleted -= OnCashPaymentCompleted;
        _arePaymentEventsSubscribed = false;
    }

    private void OpenReceiptAfterPayment()
    {
        if (_receiptPreviewState.HasReceipt)
        {
            _receiptDialogHost.ShowOrActivate();
        }
    }

    private void OnOpenReceipt(object sender, System.Windows.RoutedEventArgs e) =>
        _receiptDialogHost.ShowOrActivate();
}
