using System.Windows.Controls;
using RetailPOS.Desktop.ViewModels;
using RetailPOS.Desktop.Workflow;

namespace RetailPOS.Desktop.Views;

public partial class DashboardView : UserControl
{
    private readonly DashboardViewModel _viewModel;
    private readonly CashierWorkflowNavigator _workflowNavigator;

    public DashboardView(DashboardViewModel viewModel, CashierWorkflowNavigator workflowNavigator)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _workflowNavigator = workflowNavigator;
        DataContext = viewModel;
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, System.Windows.RoutedEventArgs e) =>
        await _viewModel.LoadAsync();

    private void OnBack(object sender, System.Windows.RoutedEventArgs e) =>
        _workflowNavigator.Reset(CashierWorkflowScreen.Register);
}
