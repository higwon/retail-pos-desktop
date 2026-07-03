using RetailPOS.Desktop.ViewModels;
using System.Windows.Controls;

namespace RetailPOS.Desktop.Views;

public partial class PosMainView : UserControl
{
    private readonly Func<CustomerDisplayWindow> _customerDisplayFactory;
    private readonly Func<PaymentDialog> _paymentDialogFactory;
    private readonly Func<ReceiptDialog> _receiptDialogFactory;

    public PosMainView(
        PosMainViewModel viewModel,
        ProductGridView productGrid,
        CartPanelView cartPanel,
        Func<CustomerDisplayWindow> customerDisplayFactory,
        Func<PaymentDialog> paymentDialogFactory,
        Func<ReceiptDialog> receiptDialogFactory)
    {
        InitializeComponent();
        DataContext = viewModel;
        _customerDisplayFactory = customerDisplayFactory;
        _paymentDialogFactory = paymentDialogFactory;
        _receiptDialogFactory = receiptDialogFactory;
        ProductRegion.Content = productGrid;
        CartRegion.Content = cartPanel;
    }

    private void OnOpenCustomerDisplay(object sender, System.Windows.RoutedEventArgs e)
    {
        _customerDisplayFactory().Show();
    }

    private void OnOpenPayment(object sender, System.Windows.RoutedEventArgs e) =>
        _paymentDialogFactory().ShowDialog();

    private void OnOpenReceipt(object sender, System.Windows.RoutedEventArgs e) =>
        _receiptDialogFactory().ShowDialog();
}
