using System.Windows.Controls;
using RetailPOS.Desktop.ViewModels;
using RetailPOS.Desktop.Workflow;

namespace RetailPOS.Desktop.Views;

public partial class CheckoutRecoveryView : UserControl
{
    private readonly CheckoutRecoveryViewModel _viewModel;
    private readonly CashierWorkflowNavigator _workflowNavigator;
    private bool _loadedOnce;

    public CheckoutRecoveryView(
        CheckoutRecoveryViewModel viewModel,
        CashierWorkflowNavigator workflowNavigator)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _workflowNavigator = workflowNavigator;
        DataContext = viewModel;
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (_loadedOnce)
        {
            return;
        }

        _loadedOnce = true;
        Loaded -= OnLoaded;
        await _viewModel.LoadAsync();
    }

    private void OnBack(object sender, System.Windows.RoutedEventArgs e) =>
        _workflowNavigator.Reset(CashierWorkflowScreen.Register);
}
