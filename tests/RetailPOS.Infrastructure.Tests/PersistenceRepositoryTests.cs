using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RetailPOS.Application.Checkout;
using RetailPOS.Application.Persistence;
using RetailPOS.Application.Products;
using RetailPOS.Application.Sync;
using RetailPOS.Domain.Orders;
using RetailPOS.Domain.Payments;
using RetailPOS.Infrastructure.DependencyInjection;
using RetailPOS.Infrastructure.Persistence;
using RetailPOS.Infrastructure.Persistence.Entities;
using System.Diagnostics;
using Xunit.Abstractions;

namespace RetailPOS.Infrastructure.Tests;

public sealed class PersistenceRepositoryTests
{
    private readonly ITestOutputHelper _output;

    public PersistenceRepositoryTests(ITestOutputHelper output) => _output = output;

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
    public async Task LargeDeterministicDataset_KeepsCashierCriticalQueriesBounded()
    {
        await using var harness = await PersistenceHarness.CreateAsync();
        var db = harness.Services.GetRequiredService<LocalPosDbContext>();
        var now = new DateTime(2026, 7, 11, 3, 0, 0, DateTimeKind.Utc);
        db.Products.AddRange(Enumerable.Range(1, 5000).Select(index => new ProductEntity
        {
            Id = DeterministicGuid(index),
            Sku = $"PERF-{index:00000}",
            Barcode = $"99000{index:0000000}",
            Name = index == 4201 ? "Target Cleanser 04201" : $"Performance Product {index:00000}",
            CategoryName = $"Category {index % 20:00}",
            UnitPrice = 1000m + index,
            StockQuantity = index % 100,
            IsActive = index % 5 != 0,
            Version = 1,
            UpdatedUtc = now
        }));
        db.SyncQueue.AddRange(Enumerable.Range(1, 2000).Select(index => new SyncQueueEntity
        {
            Id = DeterministicGuid(10000 + index),
            ItemType = "Order",
            AggregateId = DeterministicGuid(20000 + index),
            ReferenceKey = $"performance-{index:00000}",
            Status = (int)SyncQueueStatus.Pending,
            RetryCount = index % 4,
            NextAttemptAtUtc = now.AddSeconds(index % 60),
            CreatedAtUtc = now.AddSeconds(index),
            UpdatedAtUtc = now.AddSeconds(index)
        }));
        var businessDate = new DateOnly(2026, 7, 11);
        db.Orders.AddRange(Enumerable.Range(1, 2000).Select(index => PerformanceOrder(index, businessDate, now)));
        db.PendingCheckouts.AddRange(Enumerable.Range(1, 1000).Select(index => new PendingCheckoutEntity
        {
            Id = DeterministicGuid(30000 + index),
            StoreId = DeterministicGuid(40001),
            TerminalId = DeterministicGuid(40002),
            CashierId = DeterministicGuid(40003),
            CreatedAtUtc = now.AddSeconds(-index),
            RecoveryStatus = (int)PendingCheckoutStatus.ManagerReviewRequired,
            CartSnapshotJson = PerformanceCartSnapshot,
            PaymentSnapshotJson = "{}",
            PaymentStatus = (int)PaymentStatus.Unknown,
            LastUpdatedAtUtc = now.AddSeconds(-index)
        }));
        await db.SaveChangesAsync();
        var products = harness.Services.GetRequiredService<IProductRepository>();
        var queue = harness.Services.GetRequiredService<ISyncQueueRepository>();
        var orders = harness.Services.GetRequiredService<IOrderRepository>();
        var dashboard = harness.Services.GetRequiredService<IDashboardRepository>();
        var pendingCheckouts = harness.Services.GetRequiredService<IPendingCheckoutRepository>();
        var clock = new StubCheckoutClock(new DateTimeOffset(now));
        var recovery = new CheckoutRecoveryService(pendingCheckouts, new UnusedOrderCompletionService(), clock);
        var syncStatus = new SyncStatusService(queue, new StubOrderSyncClock(new DateTimeOffset(now)));

        var active = await MeasureAsync("active catalog load", () => products.GetActiveAsync());
        var barcode = await MeasureAsync("barcode lookup", () => products.GetByBarcodeAsync("990000004201"));
        var search = await MeasureAsync("product search", () => products.SearchAsync("Target Cleanser"));
        var due = await MeasureAsync("due sync selection", () =>
            queue.GetDuePendingAsync(new DateTimeOffset(now.AddMinutes(1)), 100));
        var businessDayOrders = await MeasureAsync("business-day order history", () =>
            orders.GetByBusinessDateAsync(businessDate));
        var recentOrders = await MeasureAsync("recent order selection", () => orders.GetRecentAsync(5));
        var recoverable = await MeasureAsync("checkout recovery history", () => recovery.GetRecoverableAsync());
        var status = await MeasureAsync("sync status history", () => syncStatus.GetSnapshotAsync(50));
        var dashboardSummary = await MeasureAsync("bounded dashboard summary", () =>
            dashboard.GetSummaryAsync(businessDate, 5));

        Assert.Equal(4006, active.Value.Count);
        Assert.NotNull(barcode.Value);
        Assert.Single(search.Value);
        Assert.Equal(100, due.Value.Count);
        Assert.Equal(2000, businessDayOrders.Value.Count);
        Assert.Equal(5, recentOrders.Value.Count);
        Assert.Equal(1000, recoverable.Value.Count);
        Assert.Equal(50, status.Value.Items.Count);
        Assert.Equal(2000, dashboardSummary.Value.OrderCount);
        Assert.Equal(4_001_000m, dashboardSummary.Value.NetSales);
        Assert.Equal(1000, dashboardSummary.Value.RecoverableCheckoutCount);
        Assert.Equal(5, dashboardSummary.Value.RecentOrders.Count);
        Assert.Equal("PERF-ORDER-00001", dashboardSummary.Value.RecentOrders[0].LocalOrderNumber);
        Assert.All(new[]
        {
            active.Elapsed,
            barcode.Elapsed,
            search.Elapsed,
            due.Elapsed,
            businessDayOrders.Elapsed,
            recentOrders.Elapsed,
            recoverable.Elapsed,
            status.Elapsed,
            dashboardSummary.Elapsed
        }, elapsed =>
            Assert.True(elapsed < TimeSpan.FromSeconds(5), $"Query exceeded baseline ceiling: {elapsed}."));
    }

    private async Task<(T Value, TimeSpan Elapsed)> MeasureAsync<T>(string name, Func<Task<T>> action)
    {
        var stopwatch = Stopwatch.StartNew();
        var value = await action();
        stopwatch.Stop();
        _output.WriteLine($"{name}: {stopwatch.Elapsed.TotalMilliseconds:N1} ms");
        return (value, stopwatch.Elapsed);
    }

    private static Guid DeterministicGuid(int value) =>
        Guid.Parse($"00000000-0000-0000-0000-{value:000000000000}");

    private const string PerformanceCartSnapshot =
        """
        {"lines":[{"productId":"00000000-0000-0000-0000-000000000001","productName":"Performance Product","unitPrice":1000,"quantity":1,"lineTotal":1000}],"subtotal":1000,"discountType":null,"discountValue":null,"discountAmount":0,"total":1000}
        """;

    private static OrderEntity PerformanceOrder(int index, DateOnly businessDate, DateTime now)
    {
        var orderId = DeterministicGuid(50000 + index);
        var createdAt = now.AddSeconds(-index);
        return new OrderEntity
        {
            LocalOrderId = orderId,
            LocalOrderNumber = $"PERF-ORDER-{index:00000}",
            StoreId = DeterministicGuid(40001),
            TerminalId = DeterministicGuid(40002),
            CashierId = DeterministicGuid(40003),
            BusinessDate = businessDate,
            CreatedAtUtc = createdAt,
            Status = (int)OrderStatus.Completed,
            SubtotalAmount = 1000m + index,
            TotalAmount = 1000m + index,
            Lines =
            [
                new OrderLineEntity
                {
                    Id = DeterministicGuid(60000 + index),
                    LocalOrderId = orderId,
                    ProductId = DeterministicGuid(index),
                    ProductNameSnapshot = $"Performance Product {index:00000}",
                    UnitPrice = 1000m + index,
                    Quantity = 1,
                    GrossAmount = 1000m + index,
                    LineTotalAmount = 1000m + index
                }
            ],
            Payments =
            [
                new PaymentEntity
                {
                    Id = DeterministicGuid(70000 + index),
                    LocalOrderId = orderId,
                    Method = (int)PaymentMethod.Card,
                    Status = (int)PaymentStatus.Approved,
                    RequestedAmount = 1000m + index,
                    ApprovedAmount = 1000m + index,
                    CreatedAtUtc = createdAt,
                    ApprovedAtUtc = createdAt,
                    ApprovalCode = $"APP-{index:00000}",
                    TransactionReference = $"TX-{index:00000}"
                }
            ]
        };
    }

    [Fact]
    public async Task DashboardRepository_CountsOnlyRecoverableCheckoutStates()
    {
        await using var harness = await PersistenceHarness.CreateAsync();
        var db = harness.Services.GetRequiredService<LocalPosDbContext>();
        var now = new DateTime(2026, 7, 11, 3, 0, 0, DateTimeKind.Utc);
        var statuses = new[]
        {
            PendingCheckoutStatus.AwaitingPayment,
            PendingCheckoutStatus.ApprovedButOrderNotCreated,
            PendingCheckoutStatus.ManagerReviewRequired,
            PendingCheckoutStatus.PaymentFailed,
            PendingCheckoutStatus.Completed,
            PendingCheckoutStatus.ReviewResolved
        };
        db.PendingCheckouts.AddRange(statuses.Select((status, index) => new PendingCheckoutEntity
        {
            Id = DeterministicGuid(80000 + index),
            StoreId = DeterministicGuid(40001),
            TerminalId = DeterministicGuid(40002),
            CashierId = DeterministicGuid(40003),
            CreatedAtUtc = now.AddSeconds(index),
            RecoveryStatus = (int)status,
            CartSnapshotJson = PerformanceCartSnapshot,
            PaymentSnapshotJson = "{}",
            PaymentStatus = (int)PaymentStatus.Unknown,
            LastUpdatedAtUtc = now.AddSeconds(index)
        }));
        await db.SaveChangesAsync();

        var summary = await harness.Services.GetRequiredService<IDashboardRepository>()
            .GetSummaryAsync(new DateOnly(2026, 7, 11), 5);

        Assert.Equal(3, summary.RecoverableCheckoutCount);
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
    public async Task PendingCheckoutRepository_ExcludesResolvedReviewFromUnresolvedRecords()
    {
        await using var harness = await PersistenceHarness.CreateAsync();
        var repository = harness.Services.GetRequiredService<IPendingCheckoutRepository>();
        var now = new DateTimeOffset(2026, 7, 5, 2, 0, 0, TimeSpan.Zero);
        var checkout = new PendingCheckoutRecord(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), now,
            PendingCheckoutStatus.ReviewResolved,
            "{\"items\":[]}", "{\"method\":\"Card\"}", PaymentStatus.Unknown,
            null, null, null, null, null, null, now.AddMinutes(2));

        await repository.SaveAsync(checkout);

        Assert.NotNull(await repository.GetByIdAsync(checkout.Id));
        Assert.Empty(await repository.GetUnresolvedAsync());
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
        await repository.MarkExhaustedAsync(later.Id, 5, "timeout", now.AddMinutes(3));
        var remaining = await repository.GetDuePendingAsync(now.AddMinutes(2), 10);
        Assert.Empty(remaining);

        var recent = await repository.GetRecentAsync(10);
        Assert.Equal([later.Id, first.Id, second.Id], recent.Select(item => item.Id));
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

        public Task<IReadOnlyList<SyncQueueRecord>> GetRecentAsync(
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

        public Task MarkExhaustedAsync(Guid id, int retryCount, string? lastErrorSummary,
            DateTimeOffset exhaustedAtUtc, CancellationToken cancellationToken = default) =>
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

    private sealed class StubOrderSyncClock(DateTimeOffset utcNow) : IOrderSyncClock
    {
        public DateTimeOffset UtcNow => utcNow;
    }

    private sealed class UnusedOrderCompletionService : IOrderCompletionService
    {
        public Task<OrderCompletionResult> CompleteAsync(
            Guid pendingCheckoutId,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Completion is not used by the recovery query baseline.");
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
