using RetailPOS.Desktop.ViewModels;
using System.Windows.Controls;

namespace RetailPOS.Desktop.Views;

public partial class ProductGridView : UserControl
{
    public ProductGridView(ProductGridViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
