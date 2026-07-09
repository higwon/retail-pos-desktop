using RetailPOS.Desktop.ViewModels;
using System.Windows.Controls;

namespace RetailPOS.Desktop.Views;

public partial class CartPanelView : UserControl
{
    private readonly CartPanelViewModel _viewModel;
    private bool _isCheckoutSubscribed;

    public CartPanelView(CartPanelViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public event EventHandler? CheckoutRequested;

    private void OnViewModelCheckoutRequested(object? sender, EventArgs e) =>
        CheckoutRequested?.Invoke(this, EventArgs.Empty);

    private void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (_isCheckoutSubscribed)
        {
            return;
        }

        _viewModel.CheckoutRequested += OnViewModelCheckoutRequested;
        _isCheckoutSubscribed = true;
    }

    private void OnUnloaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (!_isCheckoutSubscribed)
        {
            return;
        }

        _viewModel.CheckoutRequested -= OnViewModelCheckoutRequested;
        _isCheckoutSubscribed = false;
    }
}
