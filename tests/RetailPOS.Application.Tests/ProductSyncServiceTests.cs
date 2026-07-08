using RetailPOS.Application.Products;

namespace RetailPOS.Application.Tests;

public sealed class ProductSyncServiceTests
{
    [Fact]
    public async Task SyncAsync_FetchesPagesAndUpsertsProducts()
    {
        var firstProduct = Product("SKU-001", version: 1);
        var secondProduct = Product("SKU-002", version: 2);
        var serverTime = new DateTimeOffset(2026, 7, 8, 1, 0, 0, TimeSpan.Zero);
        var client = new StubProductSyncClient(
            new ProductSyncPage([firstProduct], 1, 100, HasMore: true, serverTime),
            new ProductSyncPage([secondProduct], 2, 100, HasMore: false, serverTime.AddMinutes(1)));
        var store = new StubProductSyncStore();
        var service = new ProductSyncService(client, store);

        var result = await service.SyncAsync(null, pageSize: 100);

        Assert.Equal(2, result.UpsertedCount);
        Assert.Equal(serverTime.AddMinutes(1), result.ServerTimeUtc);
        Assert.Equal([1, 2], client.Requests.Select(request => request.Page));
        Assert.Equal([firstProduct, secondProduct], store.Products);
    }

    [Fact]
    public async Task SyncAsync_RejectsNonUtcCursor()
    {
        var service = new ProductSyncService(
            new StubProductSyncClient(),
            new StubProductSyncStore());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.SyncAsync(new DateTimeOffset(2026, 7, 8, 10, 0, 0, TimeSpan.FromHours(9)), 100));
    }

    private static ProductSyncItem Product(string sku, long version) => new(
        Guid.NewGuid(),
        sku,
        $"BAR-{sku}",
        $"Product {sku}",
        "Category",
        1000m,
        StockQuantity: 10,
        IsActive: true,
        version,
        new DateTimeOffset(2026, 7, 8, 0, 0, 0, TimeSpan.Zero).AddMinutes(version));

    private sealed class StubProductSyncClient(params ProductSyncPage[] pages) : IProductSyncClient
    {
        private readonly Queue<ProductSyncPage> _pages = new(pages);

        public List<ProductSyncPageRequest> Requests { get; } = [];

        public Task<ProductSyncPage> GetProductsAsync(
            ProductSyncPageRequest request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return Task.FromResult(_pages.Dequeue());
        }
    }

    private sealed class StubProductSyncStore : IProductSyncStore
    {
        public List<ProductSyncItem> Products { get; } = [];

        public Task<int> UpsertAsync(
            IReadOnlyList<ProductSyncItem> products,
            CancellationToken cancellationToken = default)
        {
            Products.AddRange(products);
            return Task.FromResult(products.Count);
        }
    }
}
