using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RetailPOS.Application.Checkout;
using RetailPOS.Application.Persistence;
using RetailPOS.Application.Products;
using RetailPOS.Domain.Products;
using RetailPOS.Desktop.Workflow;
using System.Collections.ObjectModel;

namespace RetailPOS.Desktop.ViewModels;

public sealed partial class ProductGridViewModel : ObservableObject
{
    private const int ProductPageSize = 50;

    private readonly IProductRepository _productRepository;
    private readonly CheckoutSession _checkoutSession;
    private readonly CashierWorkflowNavigator? _workflowNavigator;
    private readonly AsyncRelayCommand _searchCommand;
    private readonly AsyncRelayCommand _scanBarcodeCommand;
    private bool _isLoaded;
    private readonly List<Product> _allProducts = [];
    private readonly List<Product> _filteredProducts = [];
    private int _visibleProductCount;

    public ProductGridViewModel(
        IProductRepository productRepository,
        CheckoutSession checkoutSession,
        CashierWorkflowNavigator? workflowNavigator = null)
    {
        _productRepository = productRepository;
        _checkoutSession = checkoutSession;
        _workflowNavigator = workflowNavigator;
        _searchCommand = new AsyncRelayCommand(SearchAsync);
        _scanBarcodeCommand = new AsyncRelayCommand(ScanBarcodeAsync);
        AddProductCommand = new RelayCommand<Product>(AddProduct);
        CancelCommand = new RelayCommand(ReturnToRegister);
        LoadMoreProductsCommand = new RelayCommand(LoadMoreProducts, () => HasMoreProducts);
    }

    public ObservableCollection<Product> Products { get; } = [];
    public ObservableCollection<string> Categories { get; } = [];
    public IAsyncRelayCommand SearchCommand => _searchCommand;
    public IRelayCommand<Product> AddProductCommand { get; }
    public IRelayCommand CancelCommand { get; }
    public IRelayCommand LoadMoreProductsCommand { get; }
    public IAsyncRelayCommand ScanBarcodeCommand => _scanBarcodeCommand;

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
    private string _barcodeText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasBarcodeMessage))]
    private string? _barcodeMessage;

    [ObservableProperty]
    private Product? _selectedProduct;

    [ObservableProperty]
    private string _selectedCategory = ProductCatalogCategories.All;

    partial void OnSelectedCategoryChanged(string value) => ApplyFilters();

    public bool HasProducts => !IsLoading && Products.Count > 0;
    public bool IsEmpty => !IsLoading && !HasError && Products.Count == 0;
    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);
    public bool HasBarcodeMessage => !string.IsNullOrEmpty(BarcodeMessage);
    public bool HasMoreProducts => Products.Count < _filteredProducts.Count;
    public string ProductResultsText => _filteredProducts.Count == 0
        ? "No products"
        : $"Showing {Products.Count:N0} of {_filteredProducts.Count:N0} products";

    private void AddProduct(Product? product)
    {
        if (product is null)
        {
            return;
        }

        SelectedProduct = product;
        _checkoutSession.AddProduct(product);
        ReturnToRegister();
    }

    private void ReturnToRegister()
    {
        if (_workflowNavigator?.Current == CashierWorkflowScreen.ProductSearch)
        {
            _workflowNavigator.GoBack();
        }
    }

    public async Task LoadAsync()
    {
        if (_isLoaded)
        {
            return;
        }

        await _searchCommand.ExecuteAsync(null);
    }

    private async Task SearchAsync(CancellationToken cancellationToken)
    {
        IsLoading = true;
        ErrorMessage = null;
        BarcodeMessage = null;
        SelectedProduct = null;

        try
        {
            if (!_isLoaded)
            {
                _allProducts.Clear();
                _allProducts.AddRange(await _productRepository.GetActiveAsync(cancellationToken));
                Categories.Clear();
                foreach (var category in ProductCatalogCategories.From(_allProducts)) Categories.Add(category);
                SelectedCategory = ProductCatalogCategories.All;
                _isLoaded = true;
            }
            ApplyFilters();
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

    private void ApplyFilters()
    {
        if (!_isLoaded) return;
        var keyword = SearchText.Trim();
        _filteredProducts.Clear();
        _filteredProducts.AddRange(_allProducts.Where(product =>
            (SelectedCategory == ProductCatalogCategories.All ||
             string.Equals(product.CategoryName, SelectedCategory, StringComparison.OrdinalIgnoreCase)) &&
            (keyword.Length == 0 ||
             product.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
             product.Sku.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
             product.Barcode.Contains(keyword, StringComparison.OrdinalIgnoreCase))));
        _visibleProductCount = Math.Min(ProductPageSize, _filteredProducts.Count);
        PresentProducts();
    }

    private void LoadMoreProducts()
    {
        _visibleProductCount = Math.Min(
            _visibleProductCount + ProductPageSize,
            _filteredProducts.Count);
        PresentProducts();
    }

    private void PresentProducts()
    {
        Products.Clear();
        foreach (var product in _filteredProducts.Take(_visibleProductCount)) Products.Add(product);
        OnPropertyChanged(nameof(HasProducts));
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(HasMoreProducts));
        OnPropertyChanged(nameof(ProductResultsText));
        LoadMoreProductsCommand.NotifyCanExecuteChanged();
    }

    private async Task ScanBarcodeAsync(CancellationToken cancellationToken)
    {
        var barcode = BarcodeText.Trim();
        if (string.IsNullOrEmpty(barcode))
        {
            return;
        }

        var found = await ProcessBarcodeAsync(barcode, cancellationToken);
        if (found)
        {
            BarcodeText = string.Empty;
        }
    }

    public async Task<bool> ProcessBarcodeAsync(
        string barcode,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        barcode = barcode.Trim();
        if (string.IsNullOrEmpty(barcode))
        {
            return false;
        }

        BarcodeMessage = null;

        try
        {
            var product = await _productRepository.GetByBarcodeAsync(barcode, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (product is null)
            {
                BarcodeMessage = "Product barcode was not found. Cart was not changed.";
                return false;
            }

            SelectedProduct = product;
            _checkoutSession.AddProduct(product);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return false;
        }
        catch
        {
            BarcodeMessage = "Barcode lookup could not be completed. Try again.";
            return false;
        }
    }
}
