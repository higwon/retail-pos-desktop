using RetailPOS.Application.Persistence;
using RetailPOS.Application.Checkout;
using RetailPOS.Desktop.ViewModels;
using RetailPOS.Domain.Products;
using System.Diagnostics;

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
        var repository = new StubProductRepository { ActiveProducts = [Product("Water"), expected] };
        var viewModel = new ProductGridViewModel(repository, new CheckoutSession()) { SearchText = "  cola  " };

        await viewModel.SearchCommand.ExecuteAsync(null);
        viewModel.SelectedProduct = viewModel.Products.Single();

        Assert.Equal(expected.Id, Assert.Single(viewModel.Products).Id);
        Assert.Same(expected, viewModel.SelectedProduct);
    }

    [Fact]
    public async Task CategoryAndSearch_ComposeFromSharedCategorySource()
    {
        var cola = Product("Cola", category: "Drinks");
        var water = Product("Water", category: "Drinks");
        var noodles = Product("Cup Noodles", category: "Food");
        var viewModel = new ProductGridViewModel(
            new StubProductRepository { ActiveProducts = [cola, water, noodles] },
            new CheckoutSession());
        await viewModel.LoadAsync();

        Assert.Equal(["All categories", "Drinks", "Food"], viewModel.Categories);
        viewModel.SelectedCategory = "Drinks";
        Assert.Equal(2, viewModel.Products.Count);

        viewModel.SearchText = "cola";
        await viewModel.SearchCommand.ExecuteAsync(null);
        Assert.Equal(cola.Id, Assert.Single(viewModel.Products).Id);

        viewModel.SelectedCategory = "Food";
        Assert.Empty(viewModel.Products);
    }

    [Fact]
    public async Task ProductTileCommand_RepeatedActivationAddsExactlyOneUnitEachTime()
    {
        var product = Product("Cola");
        var session = new CheckoutSession();
        var viewModel = new ProductGridViewModel(
            new StubProductRepository { ActiveProducts = [product] }, session);
        await viewModel.LoadAsync();

        viewModel.AddProductCommand.Execute(product);
        viewModel.AddProductCommand.Execute(product);
        viewModel.AddProductCommand.Execute(product);

        Assert.Equal(3, Assert.Single(session.Snapshot.Lines).Quantity);
    }

    [Fact]
    public async Task LargeCatalog_CategoryAndSearchFilterRemainBounded()
    {
        var products = Enumerable.Range(1, 5000)
            .Select(index => Product(
                $"Product {index:00000}",
                barcode: $"99{index:0000000000}",
                category: $"Category {index % 20:00}"))
            .ToArray();
        var viewModel = new ProductGridViewModel(
            new StubProductRepository { ActiveProducts = products },
            new CheckoutSession());
        await viewModel.LoadAsync();

        Assert.Equal(50, viewModel.Products.Count);
        Assert.True(viewModel.HasMoreProducts);
        Assert.Equal("Showing 50 of 5,000 products", viewModel.ProductResultsText);
        viewModel.LoadMoreProductsCommand.Execute(null);
        Assert.Equal(100, viewModel.Products.Count);

        var stopwatch = Stopwatch.StartNew();
        viewModel.SelectedCategory = "Category 07";
        viewModel.SearchText = "Product 04";
        await viewModel.SearchCommand.ExecuteAsync(null);
        stopwatch.Stop();

        Assert.Equal(50, viewModel.Products.Count);
        Assert.False(viewModel.HasMoreProducts);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5),
            $"Category/search filter exceeded baseline ceiling: {stopwatch.Elapsed}.");
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

    private static Product Product(string name, string? barcode = null, string category = "Beverages") => new(
        Guid.NewGuid(),
        $"SKU-{name}",
        barcode ?? Guid.NewGuid().ToString("N"),
        name,
        category,
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
