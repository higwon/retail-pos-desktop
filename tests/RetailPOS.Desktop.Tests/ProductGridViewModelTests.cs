using RetailPOS.Application.Persistence;
using RetailPOS.Application.Checkout;
using RetailPOS.Desktop.ViewModels;
using RetailPOS.Domain.Products;

namespace RetailPOS.Desktop.Tests;

public sealed class ProductGridViewModelTests
{
    [Fact]
    public async Task LoadAsync_LoadsActiveProductsOnlyOnce()
    {
        var repository = new StubProductRepository
        {
            ActiveProducts = [Product("Water"), Product("Cola")]
        };
        var viewModel = new ProductGridViewModel(repository, new CheckoutSession());

        await viewModel.LoadAsync();
        await viewModel.LoadAsync();

        Assert.Equal(2, viewModel.Products.Count);
        Assert.Equal(1, repository.GetActiveCalls);
        Assert.True(viewModel.HasProducts);
    }

    [Fact]
    public async Task SearchCommand_UsesTrimmedKeywordAndExposesSelection()
    {
        var expected = Product("Cola");
        var repository = new StubProductRepository { SearchProducts = [expected] };
        var viewModel = new ProductGridViewModel(repository, new CheckoutSession()) { SearchText = "  cola  " };

        await viewModel.SearchCommand.ExecuteAsync(null);
        viewModel.SelectedProduct = viewModel.Products.Single();

        Assert.Equal("cola", repository.LastKeyword);
        Assert.Same(expected, viewModel.SelectedProduct);
    }

    [Fact]
    public async Task ScanBarcodeCommand_WhenBarcodeIsKnown_AddsProductToCart()
    {
        var expected = Product("Cola", barcode: "8801000000011");
        var session = new CheckoutSession();
        var repository = new StubProductRepository { BarcodeProduct = expected };
        var viewModel = new ProductGridViewModel(repository, session) { BarcodeText = "  8801000000011  " };

        await viewModel.ScanBarcodeCommand.ExecuteAsync(null);

        var line = Assert.Single(session.Snapshot.Lines);
        Assert.Equal(expected.Id, line.ProductId);
        Assert.Equal("8801000000011", repository.LastBarcode);
        Assert.Same(expected, viewModel.SelectedProduct);
        Assert.Equal(string.Empty, viewModel.BarcodeText);
        Assert.False(viewModel.HasBarcodeMessage);
    }

    [Fact]
    public async Task ScanBarcodeCommand_WhenBarcodeIsUnknown_ShowsSafeMessageWithoutChangingCart()
    {
        var existing = Product("Water");
        var session = new CheckoutSession();
        session.AddProduct(existing);
        var repository = new StubProductRepository();
        var viewModel = new ProductGridViewModel(repository, session) { BarcodeText = "  missing-code  " };

        await viewModel.ScanBarcodeCommand.ExecuteAsync(null);

        var line = Assert.Single(session.Snapshot.Lines);
        Assert.Equal(existing.Id, line.ProductId);
        Assert.Equal("missing-code", repository.LastBarcode);
        Assert.True(viewModel.HasBarcodeMessage);
        Assert.DoesNotContain("Exception", viewModel.BarcodeMessage);
    }

    [Fact]
    public async Task ProcessBarcodeAsync_ForScannerDoesNotMutateManualBarcodeText()
    {
        var expected = Product("Cola", barcode: "known");
        var session = new CheckoutSession();
        var viewModel = new ProductGridViewModel(
            new StubProductRepository { BarcodeProduct = expected },
            session)
        {
            BarcodeText = "manual-fallback"
        };

        var found = await viewModel.ProcessBarcodeAsync(" known ");

        Assert.True(found);
        Assert.Equal("manual-fallback", viewModel.BarcodeText);
        Assert.Single(session.Snapshot.Lines);
    }

    [Fact]
    public async Task SearchCommand_WhenRepositoryFails_ShowsSafeErrorState()
    {
        var viewModel = new ProductGridViewModel(
            new StubProductRepository { Exception = new InvalidOperationException() },
            new CheckoutSession());

        await viewModel.SearchCommand.ExecuteAsync(null);

        Assert.True(viewModel.HasError);
        Assert.False(viewModel.IsLoading);
        Assert.Empty(viewModel.Products);
        Assert.DoesNotContain("InvalidOperationException", viewModel.ErrorMessage);
    }

    private static Product Product(string name, string? barcode = null) => new(
        Guid.NewGuid(),
        $"SKU-{name}",
        barcode ?? Guid.NewGuid().ToString("N"),
        name,
        "Beverages",
        1000m);

    private sealed class StubProductRepository : IProductRepository
    {
        public IReadOnlyList<Product> ActiveProducts { get; init; } = [];
        public IReadOnlyList<Product> SearchProducts { get; init; } = [];
        public Product? BarcodeProduct { get; init; }
        public Exception? Exception { get; init; }
        public int GetActiveCalls { get; private set; }
        public string? LastKeyword { get; private set; }
        public string? LastBarcode { get; private set; }

        public Task<IReadOnlyList<Product>> GetActiveAsync(CancellationToken cancellationToken = default)
        {
            GetActiveCalls++;
            return Result(ActiveProducts);
        }

        public Task<IReadOnlyList<Product>> SearchAsync(string keyword, CancellationToken cancellationToken = default)
        {
            LastKeyword = keyword;
            return Result(SearchProducts);
        }

        public Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<Product?>(null);

        public Task<Product?> GetByBarcodeAsync(string barcode, CancellationToken cancellationToken = default)
        {
            LastBarcode = barcode;
            return Exception is null
                ? Task.FromResult(BarcodeProduct)
                : Task.FromException<Product?>(Exception);
        }

        private Task<IReadOnlyList<Product>> Result(IReadOnlyList<Product> products) =>
            Exception is null
                ? Task.FromResult(products)
                : Task.FromException<IReadOnlyList<Product>>(Exception);
    }
}
