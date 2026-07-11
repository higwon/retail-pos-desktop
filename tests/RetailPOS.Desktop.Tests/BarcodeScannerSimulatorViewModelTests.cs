using RetailPOS.Application.Persistence;
using RetailPOS.Desktop.ViewModels;
using RetailPOS.Domain.Products;
using RetailPOS.Infrastructure.Devices;

namespace RetailPOS.Desktop.Tests;

public sealed class BarcodeScannerSimulatorViewModelTests
{
    private static readonly Product Cola = Product("Cola", "SKU-COLA", "8801", "Drinks");
    private static readonly Product Water = Product("Water", "SKU-WATER", "8802", "Drinks");
    private static readonly Product Noodles = Product("Cup Noodles", "SKU-NOODLE", "9901", "Food");

    [Fact]
    public async Task ProductPicker_LoadsSearchesAndFiltersActiveProducts()
    {
        using var scanner = new SimulatedBarcodeScanner();
        using var viewModel = ViewModel(scanner);
        await viewModel.LoadAsync();

        Assert.Equal(3, viewModel.Products.Count);
        Assert.Equal(["All categories", "Drinks", "Food"], viewModel.Categories);

        viewModel.SearchText = "sku-noodle";
        Assert.Equal(Noodles.Id, Assert.Single(viewModel.Products).Id);

        viewModel.SearchText = string.Empty;
        viewModel.SelectedCategory = "Drinks";
        Assert.Equal(2, viewModel.Products.Count);
    }

    [Fact]
    public async Task SelectedProduct_CanBeEmittedRepeatedlyWithoutLosingSelection()
    {
        using var scanner = new SimulatedBarcodeScanner();
        using var viewModel = ViewModel(scanner);
        var scans = new List<string>();
        scanner.BarcodeScanned += (_, args) => scans.Add(args.Barcode);
        await viewModel.LoadAsync();
        viewModel.SelectedProduct = Cola;

        await viewModel.EmitScanCommand.ExecuteAsync(null);
        await viewModel.EmitScanCommand.ExecuteAsync(null);

        Assert.Equal(["8801", "8801"], scans);
        Assert.Same(Cola, viewModel.SelectedProduct);
        Assert.Equal("8801", viewModel.SelectedBarcodePreview);
    }

    [Fact]
    public async Task ManualMode_PreservesUnknownBarcodeTestingAndConnectionRules()
    {
        using var scanner = new SimulatedBarcodeScanner();
        using var viewModel = ViewModel(scanner);
        viewModel.Mode = BarcodeScannerInputMode.ManualBarcode;
        viewModel.Barcode = "UNKNOWN-1";

        Assert.True(viewModel.EmitScanCommand.CanExecute(null));
        viewModel.DisconnectCommand.Execute(null);
        Assert.False(viewModel.EmitScanCommand.CanExecute(null));
        viewModel.ConnectCommand.Execute(null);
        await viewModel.EmitScanCommand.ExecuteAsync(null);
        Assert.Contains("UNKNOWN-1", viewModel.StatusMessage);
    }

    [Fact]
    public async Task BackgroundToggle_UsesExistingScannerCallbackPath()
    {
        using var scanner = new SimulatedBarcodeScanner();
        using var viewModel = ViewModel(scanner);
        var callback = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        scanner.BarcodeScanned += (_, _) => callback.TrySetResult();
        await viewModel.LoadAsync();
        viewModel.SelectedProduct = Water;
        viewModel.EmitOnBackgroundThread = true;

        await viewModel.EmitScanCommand.ExecuteAsync(null);

        await callback.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.True(scanner.EmitOnBackgroundThread);
    }

    [Fact]
    public async Task FailedLoad_CanBeRetriedSuccessfully()
    {
        using var scanner = new SimulatedBarcodeScanner();
        var repository = new RetryProductRepository([Cola, Water]);
        repository.FailNext = true;
        using var viewModel = new BarcodeScannerSimulatorViewModel(scanner, repository);

        await viewModel.LoadAsync();
        Assert.Empty(viewModel.Products);
        Assert.NotNull(viewModel.ErrorMessage);

        await viewModel.LoadAsync();
        Assert.Equal(2, viewModel.Products.Count);
        Assert.Equal(["All categories", "Drinks"], viewModel.Categories);
        Assert.Null(viewModel.ErrorMessage);
        Assert.Equal(2, repository.Calls);
    }

    [Fact]
    public async Task CancelledLoad_CanBeRetriedSuccessfully()
    {
        using var scanner = new SimulatedBarcodeScanner();
        var repository = new RetryProductRepository([Noodles]) { CancelNext = true };
        using var viewModel = new BarcodeScannerSimulatorViewModel(scanner, repository);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await viewModel.LoadAsync(cancellation.Token);
        Assert.Empty(viewModel.Products);

        await viewModel.LoadAsync();
        Assert.Single(viewModel.Products);
        Assert.Equal(2, repository.Calls);
    }

    private static BarcodeScannerSimulatorViewModel ViewModel(SimulatedBarcodeScanner scanner) =>
        new(scanner, new StubProductRepository([Cola, Water, Noodles]));

    private static Product Product(string name, string sku, string barcode, string category) =>
        new(Guid.NewGuid(), sku, barcode, name, category, 1000m);

    private sealed class StubProductRepository(IReadOnlyList<Product> products) : IProductRepository
    {
        public Task<IReadOnlyList<Product>> GetActiveAsync(CancellationToken cancellationToken = default) => Task.FromResult(products);
        public Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(products.SingleOrDefault(x => x.Id == id));
        public Task<Product?> GetByBarcodeAsync(string barcode, CancellationToken cancellationToken = default) => Task.FromResult(products.SingleOrDefault(x => x.Barcode == barcode));
        public Task<IReadOnlyList<Product>> SearchAsync(string keyword, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Product>>([]);
    }

    private sealed class RetryProductRepository(IReadOnlyList<Product> products) : IProductRepository
    {
        public bool FailNext { get; set; }
        public bool CancelNext { get; set; }
        public int Calls { get; private set; }

        public Task<IReadOnlyList<Product>> GetActiveAsync(CancellationToken cancellationToken = default)
        {
            Calls++;
            if (FailNext)
            {
                FailNext = false;
                throw new InvalidOperationException("Transient failure");
            }
            if (CancelNext)
            {
                CancelNext = false;
                throw new OperationCanceledException(cancellationToken);
            }
            return Task.FromResult(products);
        }

        public Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<Product?>(null);
        public Task<Product?> GetByBarcodeAsync(string barcode, CancellationToken cancellationToken = default) => Task.FromResult<Product?>(null);
        public Task<IReadOnlyList<Product>> SearchAsync(string keyword, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Product>>([]);
    }
}
