using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RetailPOS.Application.Checkout;
using RetailPOS.Application.Persistence;
using RetailPOS.Application.Products;
using RetailPOS.Domain.Orders;
using RetailPOS.Domain.Payments;
using RetailPOS.Infrastructure.DependencyInjection;
using RetailPOS.Infrastructure.Persistence;

namespace RetailPOS.Infrastructure.Tests;

public sealed class PersistenceRepositoryTests
{
    [Fact]
    public async Task ProductSeed_IsRepeatableAndReadableThroughRepository()
    {
        await using var harness = await PersistenceHarness.CreateAsync();
        var initializer = harness.Services.GetRequiredService<LocalDatabaseInitializer>();

        await initializer.InitializeAsync();
        var products = await harness.Services.GetRequiredService<IProductRepository>().GetActiveAsync();

        Assert.Equal(6, products.Count);
        Assert.All(products, product => Assert.Equal(decimal.Truncate(product.UnitPrice), product.UnitPrice));
        Assert.NotNull(await harness.Services.GetRequiredService<IProductRepository>()
            .GetByBarcodeAsync("8801000000011"));
    }

    [Fact]
    public async Task ProductSyncStore_UpsertsProductsAndKeepsInactiveCacheRecords()
    {
        await using var harness = await PersistenceHarness.CreateAsync();
        var store = harness.Services.GetRequiredService<IProductSyncStore>();
        var dbContext = harness.Services.GetRequiredService<LocalPosDbContext>();
        var productId = Guid.Parse("90000000-0000-0000-0000-000000000001");
        var updatedUtc = new DateTimeOffset(2026, 7, 8, 1, 0, 0, TimeSpan.Zero);

        var inserted = await store.UpsertAsync([
            SyncedProduct(productId, "SYNC-001", "990000000001", "Synced Product", true, version: 1, updatedUtc)
        ]);
        var updated = await store.UpsertAsync([
            SyncedProduct(productId, "SYNC-001", "990000000001", "Inactive Product", false, version: 2, updatedUtc.AddMinutes(1))
        ]);

        Assert.Equal(1, inserted);
        Assert.Equal(1, updated);
        var restored = await dbContext.Products.SingleAsync(product => product.Id == productId);
        Assert.Equal("Inactive Product", restored.Name);
        Assert.False(restored.IsActive);
        Assert.Equal(2, restored.Version);
        Assert.Equal(updatedUtc.AddMinutes(1).UtcDateTime, restored.UpdatedUtc);
    }

    [Fact]
    public async Task ProductSyncStore_IgnoresOlderProductVersions()
    {
        await using var harness = await PersistenceHarness.CreateAsync();
        var store = harness.Services.GetRequiredService<IProductSyncStore>();
        var dbContext = harness.Services.GetRequiredService<LocalPosDbContext>();
        var productId = Guid.Parse("90000000-0000-0000-0000-000000000002");
        var updatedUtc = new DateTimeOffset(2026, 7, 8, 1, 0, 0, TimeSpan.Zero);

        await store.UpsertAsync([
            SyncedProduct(productId, "SYNC-002", "990000000002", "Current Product", true, version: 5, updatedUtc)
        ]);
        var changed = await store.UpsertAsync([
            SyncedProduct(productId, "SYNC-002", "990000000002", "Old Product", true, version: 4, updatedUtc.AddMinutes(-1))
        ]);

        Assert.Equal(0, changed);
        var restored = await dbContext.Products.SingleAsync(product => product.Id == productId);
        Assert.Equal("Current Product", restored.Name);
        Assert.Equal(5, restored.Version);
    }

    [Fact]
    public async Task OrderRepository_RoundTripsCompletedOrderAggregate()
    {
        await using var harness = await PersistenceHarness.CreateAsync();
        var repository = harness.Services.GetRequiredService<IOrderRepository>();
        var createdAt = new DateTimeOffset(2026, 7, 5, 1, 2, 3, TimeSpan.Zero);
        var payment = new Payment(Guid.NewGuid(), PaymentMethod.Card, 3800m, createdAt);
        payment.Approve(3800m, createdAt.AddMinutes(1), "APP-001", "TX-001");
        var order = new Order(
            Guid.NewGuid(),
            "LOCAL-20260705-0001",
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateOnly(2026, 7, 5),
            createdAt,
            [new OrderLine(Guid.NewGuid(), "Cola 355ml", 1800m, 2, 200m)],
            [payment]);

        await repository.SaveAsync(order);
        var restored = await repository.GetByIdAsync(order.LocalOrderId);

        Assert.NotNull(restored);
        Assert.Equal(order.TotalAmount, restored.TotalAmount);
        Assert.Equal(order.StoreId, restored.StoreId);
        Assert.Equal(order.TerminalId, restored.TerminalId);
        Assert.Equal(createdAt, restored.CreatedAtUtc);
        Assert.Equal(PaymentStatus.Approved, Assert.Single(restored.Payments).Status);
        Assert.Equal("APP-001", restored.Payments[0].ApprovalCode);
    }

    [Fact]
    public async Task PendingCheckoutRepository_SavesReadsAndCompletesRecoveryRecord()
    {
        await using var harness = await PersistenceHarness.CreateAsync();
        var repository = harness.Services.GetRequiredService<IPendingCheckoutRepository>();
        var now = new DateTimeOffset(2026, 7, 5, 2, 0, 0, TimeSpan.Zero);
        var checkout = new PendingCheckoutRecord(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), now,
            PendingCheckoutStatus.ApprovedButOrderNotCreated,
            "{\"items\":[]}", "{\"method\":\"Card\"}", PaymentStatus.Approved,
            "APP-002", 5000m, "TX-002", now.AddMinutes(1), null, null, now.AddMinutes(1));

        await repository.SaveAsync(checkout);
        Assert.Single(await repository.GetUnresolvedAsync());

        var orderId = Guid.NewGuid();
        await repository.MarkCompletedAsync(checkout.Id, orderId, now.AddMinutes(2));
        var completed = await repository.GetByIdAsync(checkout.Id);

        Assert.NotNull(completed);
        Assert.Equal(PendingCheckoutStatus.Completed, completed.RecoveryStatus);
        Assert.Equal(orderId, completed.OrderId);
        Assert.Equal("TX-002", completed.TransactionReference);
        Assert.Empty(await repository.GetUnresolvedAsync());
    }

    [Fact]
    public async Task PendingCheckoutRepository_MarksManagerReviewRequired()
    {
        await using var harness = await PersistenceHarness.CreateAsync();
        var repository = harness.Services.GetRequiredService<IPendingCheckoutRepository>();
        var now = new DateTimeOffset(2026, 7, 5, 2, 0, 0, TimeSpan.Zero);
        var checkout = new PendingCheckoutRecord(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), now,
            PendingCheckoutStatus.ApprovedButOrderNotCreated,
            "{\"items\":[]}", "{\"method\":\"Card\"}", PaymentStatus.Approved,
            "APP-002", 5000m, "TX-002", now.AddMinutes(1), null, null, now.AddMinutes(1));

        await repository.SaveAsync(checkout);
        await repository.MarkManagerReviewRequiredAsync(checkout.Id, now.AddMinutes(2));

        var restored = await repository.GetByIdAsync(checkout.Id);
        Assert.NotNull(restored);
        Assert.Equal(PendingCheckoutStatus.ManagerReviewRequired, restored.RecoveryStatus);
        Assert.Equal(now.AddMinutes(2), restored.LastUpdatedAtUtc);
        Assert.Single(await repository.GetUnresolvedAsync());
    }

    [Fact]
    public async Task SyncQueueRepository_ReturnsDueItemsInDeterministicOrderAndUpdatesStatus()
    {
        await using var harness = await PersistenceHarness.CreateAsync();
        var repository = harness.Services.GetRequiredService<ISyncQueueRepository>();
        var now = new DateTimeOffset(2026, 7, 5, 3, 0, 0, TimeSpan.Zero);
        var first = QueueItem(Guid.Parse("00000000-0000-0000-0000-000000000001"), now, now);
        var second = QueueItem(Guid.Parse("00000000-0000-0000-0000-000000000002"), now, now);
        var later = QueueItem(
            Guid.Parse("00000000-0000-0000-0000-000000000003"),
            now.AddMinutes(1),
            now.AddMinutes(1));
        await repository.EnqueueAsync(later);
        await repository.EnqueueAsync(second);
        await repository.EnqueueAsync(first);

        var due = await repository.GetDuePendingAsync(now.AddMinutes(2), 10);
        Assert.Equal([first.Id, second.Id, later.Id], due.Select(item => item.Id));

        await repository.UpdateRetryAsync(first.Id, 1, now.AddMinutes(5), "offline", now.AddMinutes(2));
        await repository.MarkCompletedAsync(second.Id, now.AddMinutes(2));
        var remaining = await repository.GetDuePendingAsync(now.AddMinutes(2), 10);
        Assert.Equal(later.Id, Assert.Single(remaining).Id);
    }

    [Fact]
    public async Task LocalTransaction_RollsBackRepositoryWritesWhenOperationFails()
    {
        await using var harness = await PersistenceHarness.CreateAsync();
        var repository = harness.Services.GetRequiredService<ISyncQueueRepository>();
        var transaction = harness.Services.GetRequiredService<ILocalTransaction>();
        var now = new DateTimeOffset(2026, 7, 5, 4, 0, 0, TimeSpan.Zero);

        await Assert.ThrowsAsync<InvalidOperationException>(() => transaction.ExecuteAsync(async token =>
        {
            await repository.EnqueueAsync(QueueItem(Guid.NewGuid(), now, now), token);
            throw new InvalidOperationException("Simulated checkout failure.");
        }));

        Assert.Empty(await repository.GetDuePendingAsync(now, 10));
    }

    [Fact]
    public async Task OrderCompletion_RollsBackOrderAndPendingCompletionWhenQueueFails()
    {
        await using var harness = await PersistenceHarness.CreateAsync();
        var pendingRepository = harness.Services.GetRequiredService<IPendingCheckoutRepository>();
        var orderRepository = harness.Services.GetRequiredService<IOrderRepository>();
        var transaction = harness.Services.GetRequiredService<ILocalTransaction>();
        var now = new DateTimeOffset(2026, 7, 7, 1, 0, 0, TimeSpan.Zero);
        var approvedAt = now.AddSeconds(5);
        var pending = ApprovedCheckout(now, approvedAt);
        await pendingRepository.SaveAsync(pending);
        var service = new OrderCompletionService(
            pendingRepository,
            orderRepository,
            new ThrowingSyncQueueRepository(),
            transaction,
            new StubCheckoutClock(now.AddSeconds(8)),
            new StubCheckoutIdGenerator());

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CompleteAsync(pending.Id));

        Assert.Null(await orderRepository.GetByIdAsync(pending.OrderId!.Value));
        var restored = await pendingRepository.GetByIdAsync(pending.Id);
        Assert.NotNull(restored);
        Assert.Equal(PendingCheckoutStatus.ApprovedButOrderNotCreated, restored.RecoveryStatus);
        Assert.Null(restored.CompletedAtUtc);
    }

    private static SyncQueueRecord QueueItem(Guid id, DateTimeOffset nextAttemptAt, DateTimeOffset createdAt) =>
        new(id, "Order", Guid.NewGuid(), null, id.ToString(), SyncQueueStatus.Pending,
            0, nextAttemptAt, null, createdAt, createdAt);

    private static ProductSyncItem SyncedProduct(
        Guid id,
        string sku,
        string barcode,
        string name,
        bool isActive,
        long version,
        DateTimeOffset updatedUtc) => new(
        id,
        sku,
        barcode,
        name,
        "Synced Category",
        2500m,
        StockQuantity: 7,
        isActive,
        version,
        updatedUtc);

    private static PendingCheckoutRecord ApprovedCheckout(DateTimeOffset createdAt, DateTimeOffset approvedAt)
    {
        var orderId = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001");
        return new PendingCheckoutRecord(
            Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001"),
            Guid.Parse("10000000-0000-0000-0000-000000000001"),
            Guid.Parse("20000000-0000-0000-0000-000000000001"),
            Guid.Parse("30000000-0000-0000-0000-000000000001"),
            createdAt,
            PendingCheckoutStatus.ApprovedButOrderNotCreated,
            """
            {"lines":[{"productId":"11111111-0000-0000-0000-000000000001","productName":"Cola","unitPrice":1800,"quantity":2,"lineTotal":3600}],"subtotal":3600,"discountType":null,"discountValue":null,"discountAmount":0,"total":3600}
            """,
            """
            {"method":"Card","requestedAmount":3600,"status":"Approved","approvedAmount":3600,"approvalCode":"APP-001","transactionReference":"TX-001","approvedAtUtc":"2026-07-07T01:00:05+00:00","failureMessage":null}
            """,
            PaymentStatus.Approved,
            "APP-001",
            3600m,
            "TX-001",
            approvedAt,
            orderId,
            null,
            approvedAt);
    }

    private sealed class ThrowingSyncQueueRepository : ISyncQueueRepository
    {
        public Task EnqueueAsync(SyncQueueRecord item, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Simulated queue failure.");

        public Task<IReadOnlyList<SyncQueueRecord>> GetDuePendingAsync(
            DateTimeOffset asOfUtc,
            int count,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<SyncQueueRecord>>([]);

        public Task<bool> ExistsByReferenceKeyAsync(string referenceKey, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task UpdateRetryAsync(Guid id, int retryCount, DateTimeOffset nextAttemptAtUtc,
            string? lastErrorSummary, DateTimeOffset updatedAtUtc, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task MarkCompletedAsync(Guid id, DateTimeOffset completedAtUtc,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task MarkResolvedAsync(Guid id, DateTimeOffset resolvedAtUtc,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class StubCheckoutClock(DateTimeOffset utcNow) : ICheckoutClock
    {
        public DateTimeOffset UtcNow => utcNow;
    }

    private sealed class StubCheckoutIdGenerator : ICheckoutIdGenerator
    {
        public Guid NewId() => Guid.Parse("cccccccc-0000-0000-0000-000000000001");
    }

    private sealed class PersistenceHarness : IAsyncDisposable
    {
        private readonly ServiceProvider _provider;
        private readonly AsyncServiceScope _scope;
        private readonly string _directory;

        private PersistenceHarness(ServiceProvider provider, AsyncServiceScope scope, string directory)
        {
            _provider = provider;
            _scope = scope;
            _directory = directory;
        }

        public IServiceProvider Services => _scope.ServiceProvider;

        public static async Task<PersistenceHarness> CreateAsync()
        {
            var directory = Path.Combine(Path.GetTempPath(), "RetailPOS.Tests", Guid.NewGuid().ToString("N"));
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["LocalDatabase:DatabasePath"] = Path.Combine(directory, "retail-pos.db")
                })
                .Build();
            var services = new ServiceCollection();
            services.AddLocalPersistence(configuration);
            var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
            var scope = provider.CreateAsyncScope();
            var harness = new PersistenceHarness(provider, scope, directory);
            await harness.Services.GetRequiredService<LocalDatabaseInitializer>().InitializeAsync();
            return harness;
        }

        public async ValueTask DisposeAsync()
        {
            await _scope.DisposeAsync();
            await _provider.DisposeAsync();
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, recursive: true);
            }
        }
    }
}
