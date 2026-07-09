using RetailPOS.Desktop.ViewModels;
using System.Windows.Controls;

namespace RetailPOS.Desktop.Views;

public partial class CartPanelView : UserControl
{
    private readonly CartPanelViewModel _viewModel;

    public CartPanelView(CartPanelViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        _viewModel.CheckoutRequested += OnViewModelCheckoutRequested;
    }

    public event EventHandler? CheckoutRequested;

    private void OnViewModelCheckoutRequested(object? sender, EventArgs e) =>
        CheckoutRequested?.Invoke(this, EventArgs.Empty);
}
