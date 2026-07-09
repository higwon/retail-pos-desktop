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
        Unloaded += OnUnloaded;
    }

    public event EventHandler? CheckoutRequested;

    private void OnViewModelCheckoutRequested(object? sender, EventArgs e) =>
        CheckoutRequested?.Invoke(this, EventArgs.Empty);

    private void OnUnloaded(object sender, System.Windows.RoutedEventArgs e)
    {
        Unloaded -= OnUnloaded;
        _viewModel.CheckoutRequested -= OnViewModelCheckoutRequested;
    }
}
