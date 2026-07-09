using System.Windows.Controls;
using RetailPOS.Desktop.ViewModels;

namespace RetailPOS.Desktop.Views;

public partial class StatusView : UserControl
{
    private readonly StatusViewModel _viewModel;
    private bool _loadedOnce;

    public StatusView(StatusViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
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
}
