using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RetailPOS.Application.Persistence;
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
            "APP-002", 5000m, now.AddMinutes(1), null, null, now.AddMinutes(1));

        await repository.SaveAsync(checkout);
        Assert.Single(await repository.GetUnresolvedAsync());

        var orderId = Guid.NewGuid();
        await repository.MarkCompletedAsync(checkout.Id, orderId, now.AddMinutes(2));
        var completed = await repository.GetByIdAsync(checkout.Id);

        Assert.NotNull(completed);
        Assert.Equal(PendingCheckoutStatus.Completed, completed.RecoveryStatus);
        Assert.Equal(orderId, completed.OrderId);
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

    private static SyncQueueRecord QueueItem(Guid id, DateTimeOffset nextAttemptAt, DateTimeOffset createdAt) =>
        new(id, "Order", Guid.NewGuid(), null, id.ToString(), SyncQueueStatus.Pending,
            0, nextAttemptAt, null, createdAt, createdAt);

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
