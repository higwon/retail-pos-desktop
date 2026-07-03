using RetailPOS.Desktop.ViewModels;
using System.Windows.Controls;

namespace RetailPOS.Desktop.Views;

public partial class PosMainView : UserControl
{
    public PosMainView(PosMainViewModel viewModel, ProductGridView productGrid, CartPanelView cartPanel)
    {
        InitializeComponent();
        DataContext = viewModel;
        ProductRegion.Content = productGrid;
        CartRegion.Content = cartPanel;
    }
}
