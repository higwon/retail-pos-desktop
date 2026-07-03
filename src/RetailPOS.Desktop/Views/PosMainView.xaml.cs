using RetailPOS.Desktop.ViewModels;
using System.Windows.Controls;

namespace RetailPOS.Desktop.Views;

public partial class PosMainView : UserControl
{
    private readonly Func<CustomerDisplayWindow> _customerDisplayFactory;

    public PosMainView(
        PosMainViewModel viewModel,
        ProductGridView productGrid,
        CartPanelView cartPanel,
        Func<CustomerDisplayWindow> customerDisplayFactory)
    {
        InitializeComponent();
        DataContext = viewModel;
        _customerDisplayFactory = customerDisplayFactory;
        ProductRegion.Content = productGrid;
        CartRegion.Content = cartPanel;
    }

    private void OnOpenCustomerDisplay(object sender, System.Windows.RoutedEventArgs e)
    {
        _customerDisplayFactory().Show();
    }
}
