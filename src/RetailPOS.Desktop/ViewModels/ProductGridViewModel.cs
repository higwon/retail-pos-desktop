using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RetailPOS.Application.Checkout;
using RetailPOS.Application.Persistence;
using RetailPOS.Domain.Products;
using System.Collections.ObjectModel;

namespace RetailPOS.Desktop.ViewModels;

public sealed partial class ProductGridViewModel : ObservableObject
{
    private readonly IProductRepository _productRepository;
    private readonly CheckoutSession _checkoutSession;
    private readonly AsyncRelayCommand _searchCommand;
    private bool _isLoaded;

    public ProductGridViewModel(IProductRepository productRepository, CheckoutSession checkoutSession)
    {
        _productRepository = productRepository;
        _checkoutSession = checkoutSession;
        _searchCommand = new AsyncRelayCommand(SearchAsync);
        AddProductCommand = new RelayCommand<Product>(AddProduct);
    }

    public ObservableCollection<Product> Products { get; } = [];
    public IAsyncRelayCommand SearchCommand => _searchCommand;
    public IRelayCommand<Product> AddProductCommand { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasProducts))]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private bool _isLoading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? _errorMessage;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private Product? _selectedProduct;

    public bool HasProducts => !IsLoading && Products.Count > 0;
    public bool IsEmpty => !IsLoading && !HasError && Products.Count == 0;
    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    private void AddProduct(Product? product)
    {
        if (product is null)
        {
            return;
        }

        SelectedProduct = product;
        _checkoutSession.AddProduct(product);
    }

    public async Task LoadAsync()
    {
        if (_isLoaded)
        {
            return;
        }

        _isLoaded = true;
        await _searchCommand.ExecuteAsync(null);
    }

    private async Task SearchAsync(CancellationToken cancellationToken)
    {
        IsLoading = true;
        ErrorMessage = null;
        SelectedProduct = null;

        try
        {
            var products = string.IsNullOrWhiteSpace(SearchText)
                ? await _productRepository.GetActiveAsync(cancellationToken)
                : await _productRepository.SearchAsync(SearchText.Trim(), cancellationToken);

            Products.Clear();
            foreach (var product in products)
            {
                Products.Add(product);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch
        {
            Products.Clear();
            ErrorMessage = "Products could not be loaded. Try again.";
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(HasProducts));
            OnPropertyChanged(nameof(IsEmpty));
        }
    }
}
