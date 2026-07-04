using RetailPOS.Desktop.ViewModels;
using System.Windows.Controls;

namespace RetailPOS.Desktop.Views;

public partial class ProductGridView : UserControl
{
    private readonly ProductGridViewModel _viewModel;

    public ProductGridView(ProductGridViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, System.Windows.RoutedEventArgs e) =>
        await _viewModel.LoadAsync();
}
