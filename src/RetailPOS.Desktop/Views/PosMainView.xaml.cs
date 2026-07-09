using RetailPOS.Desktop.ViewModels;
using System.Windows.Controls;

namespace RetailPOS.Desktop.Views;

public partial class PosMainView : UserControl
{
    private readonly ReceiptPreviewState _receiptPreviewState;
    private readonly Func<CustomerDisplayWindow> _customerDisplayFactory;
    private readonly Func<PaymentDialog> _paymentDialogFactory;
    private readonly Func<ReceiptDialog> _receiptDialogFactory;
    private readonly PosMainViewModel _viewModel;

    public PosMainView(
        PosMainViewModel viewModel,
        ProductGridView productGrid,
        CartPanelView cartPanel,
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
        ProductRegion.Content = productGrid;
        CartRegion.Content = cartPanel;
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, System.Windows.RoutedEventArgs e) =>
        await _viewModel.LoadAsync();

    private void OnOpenCustomerDisplay(object sender, System.Windows.RoutedEventArgs e)
    {
        _customerDisplayFactory().Show();
    }

    private void OnOpenPayment(object sender, System.Windows.RoutedEventArgs e)
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
