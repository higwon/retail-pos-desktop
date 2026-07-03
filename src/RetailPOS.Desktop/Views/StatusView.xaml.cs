using System.Windows.Controls;
using RetailPOS.Desktop.ViewModels;

namespace RetailPOS.Desktop.Views;

public partial class StatusView : UserControl
{
    public StatusView(StatusViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
