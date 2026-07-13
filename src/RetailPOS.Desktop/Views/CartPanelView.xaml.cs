using RetailPOS.Desktop.ViewModels;
using System.Windows.Controls;

namespace RetailPOS.Desktop.Views;

public partial class CartPanelView : UserControl
{
    private readonly CartPanelViewModel _viewModel;
    private bool _arePaymentEventsSubscribed;

    public CartPanelView(CartPanelViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public event EventHandler? CardPaymentCompleted;
    public event EventHandler? CashPaymentCompleted;

    private void OnViewModelCardPaymentCompleted(object? sender, EventArgs e) =>
        CardPaymentCompleted?.Invoke(this, EventArgs.Empty);

    private void OnViewModelCashPaymentCompleted(object? sender, EventArgs e) =>
        CashPaymentCompleted?.Invoke(this, EventArgs.Empty);

    private void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (_arePaymentEventsSubscribed)
        {
            return;
        }

        _viewModel.CardPaymentCompleted += OnViewModelCardPaymentCompleted;
        _viewModel.CashPaymentCompleted += OnViewModelCashPaymentCompleted;
        _arePaymentEventsSubscribed = true;
    }

    private void OnUnloaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (!_arePaymentEventsSubscribed)
        {
            return;
        }

        _viewModel.CardPaymentCompleted -= OnViewModelCardPaymentCompleted;
        _viewModel.CashPaymentCompleted -= OnViewModelCashPaymentCompleted;
        _arePaymentEventsSubscribed = false;
    }
}
