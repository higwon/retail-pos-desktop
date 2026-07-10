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
    private readonly AsyncRelayCommand _scanBarcodeCommand;
    private bool _isLoaded;

    public ProductGridViewModel(IProductRepository productRepository, CheckoutSession checkoutSession)
    {
        _productRepository = productRepository;
        _checkoutSession = checkoutSession;
        _searchCommand = new AsyncRelayCommand(SearchAsync);
        _scanBarcodeCommand = new AsyncRelayCommand(ScanBarcodeAsync);
        AddProductCommand = new RelayCommand<Product>(AddProduct);
    }

    public ObservableCollection<Product> Products { get; } = [];
    public IAsyncRelayCommand SearchCommand => _searchCommand;
    public IRelayCommand<Product> AddProductCommand { get; }
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

    public bool HasProducts => !IsLoading && Products.Count > 0;
    public bool IsEmpty => !IsLoading && !HasError && Products.Count == 0;
    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);
    public bool HasBarcodeMessage => !string.IsNullOrEmpty(BarcodeMessage);

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
        BarcodeMessage = null;
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
        barcode = barcode.Trim();
        if (string.IsNullOrEmpty(barcode))
        {
            return false;
        }

        BarcodeMessage = null;

        try
        {
            var product = await _productRepository.GetByBarcodeAsync(barcode, cancellationToken);
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
