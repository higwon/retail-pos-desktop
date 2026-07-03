using RetailPOS.Desktop.ViewModels;
using System.Windows.Controls;

namespace RetailPOS.Desktop.Views;

public partial class CartPanelView : UserControl
{
    public CartPanelView(CartPanelViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
