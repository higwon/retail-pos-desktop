using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RetailPOS.Application.Orders;
using RetailPOS.Application.Persistence;
using RetailPOS.Application.Sync;
using RetailPOS.Infrastructure.DependencyInjection;
using RetailPOS.Infrastructure.Persistence;
using RetailPOS.Infrastructure.Sync;

namespace RetailPOS.Api.Tests;

public sealed class OrderSyncIntegrationTests
{
    private static readonly DateTimeOffset NowUtc = new(2026, 7, 9, 1, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ProcessDueAsync_UploadsSqliteQueueOrderThroughApiAndMarksCompleted()
    {
        await using var harness = await SyncIntegrationHarness.CreateAsync();
        var payload = ValidPayload();
        await harness.EnqueueAsync(payload);

        var result = await harness.SyncService.ProcessDueAsync(NowUtc, count: 10);

        Assert.Equal(1, result.ProcessedCount);
        Assert.Equal(1, result.CompletedCount);
        Assert.Equal(0, result.RetriedCount);
        Assert.Equal(0, result.ExhaustedCount);

        var item = Assert.Single(await harness.SyncQueueRepository.GetRecentAsync(10));
        Assert.Equal(SyncQueueStatus.Completed, item.Status);
        Assert.Equal(0, item.RetryCount);
        Assert.Null(item.LastErrorSummary);
        Assert.Empty(await harness.SyncQueueRepository.GetDuePendingAsync(NowUtc, 10));
    }

    [Fact]
    public async Task ProcessDueAsync_WhenApiReturnsIdempotencyConflict_MarksSqliteQueueOrderExhausted()
    {
        await using var harness = await SyncIntegrationHarness.CreateAsync();
        var firstPayload = ValidPayload();
        var conflictingPayload = firstPayload with
        {
            IdempotencyKey = "store:10000000-0000-0000-0000-000000000001:terminal:20000000-0000-0000-0000-000000000001:localOrder:conflict"
        };
        await harness.EnqueueAsync(firstPayload);
        await harness.SyncService.ProcessDueAsync(NowUtc, count: 10);
        await harness.EnqueueAsync(conflictingPayload, queueId: Guid.Parse("90000000-0000-0000-0000-000000000002"));

        var result = await harness.SyncService.ProcessDueAsync(NowUtc, count: 10);

        Assert.Equal(1, result.ProcessedCount);
        Assert.Equal(0, result.CompletedCount);
        Assert.Equal(0, result.RetriedCount);
        Assert.Equal(1, result.ExhaustedCount);

        var items = await harness.SyncQueueRepository.GetRecentAsync(10);
        var exhausted = Assert.Single(items, item => item.Id == Guid.Parse("90000000-0000-0000-0000-000000000002"));
        Assert.Equal(SyncQueueStatus.Exhausted, exhausted.Status);
        Assert.Equal(0, exhausted.RetryCount);
        Assert.Contains("idempotency conflict", exhausted.LastErrorSummary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RetryScheduledOrder_SurvivesRestartAndUploadsExactlyOnceAfterReconnect()
    {
        await using var harness = await SyncIntegrationHarness.CreateAsync();
        var queueId = Guid.Parse("90000000-0000-0000-0000-000000000001");
        await harness.EnqueueAsync(ValidPayload(), queueId);
        harness.IsOnline = false;

        var offline = await harness.SyncService.ProcessDueAsync(NowUtc, count: 10);

        Assert.Equal(1, offline.RetriedCount);
        Assert.Equal(0, offline.ExhaustedCount);
        var retry = Assert.Single(await harness.SyncQueueRepository.GetRecentAsync(10));
        Assert.Equal(SyncQueueStatus.Pending, retry.Status);
        Assert.Equal(1, retry.RetryCount);
        Assert.Equal(NowUtc.AddMinutes(1), retry.NextAttemptAtUtc);
        Assert.Contains("offline", retry.LastErrorSummary, StringComparison.OrdinalIgnoreCase);

        await harness.RestartAsync();
        harness.IsOnline = true;
        var reconnected = await harness.SyncService.ProcessDueAsync(NowUtc.AddMinutes(1), count: 10);

        Assert.Equal(1, reconnected.CompletedCount);
        var completed = Assert.Single(await harness.SyncQueueRepository.GetRecentAsync(10));
        Assert.Equal(SyncQueueStatus.Completed, completed.Status);

        await harness.RestartAsync();
        var repeatedStart = await harness.SyncService.ProcessDueAsync(NowUtc, count: 10);

        Assert.Equal(0, repeatedStart.ProcessedCount);
        Assert.Single(await harness.SyncQueueRepository.GetRecentAsync(10));
    }

    private static OrderUploadPayload ValidPayload() => new(
        OrderUploadPayload.CurrentSchemaVersion,
        Guid.Parse("10000000-0000-0000-0000-000000000001"),
        Guid.Parse("20000000-0000-0000-0000-000000000001"),
        Guid.Parse("30000000-0000-0000-0000-000000000001"),
        "store:10000000-0000-0000-0000-000000000001:terminal:20000000-0000-0000-0000-000000000001:localOrder:30000000-0000-0000-0000-000000000001",
        "POS-20260709-000001",
        new DateOnly(2026, 7, 9),
        Guid.Parse("40000000-0000-0000-0000-000000000001"),
        10000m,
        1000m,
        9000m,
        NowUtc,
        [
            new OrderUploadLinePayload(
                Guid.Parse("11111111-0000-0000-0000-000000000001"),
                "Sample Product",
                5000m,
                2,
                1000m,
                9000m)
        ],
        [
            new OrderUploadPaymentPayload(
                "Card",
                9000m,
                "APP-001",
                "TX-001",
                NowUtc.AddMinutes(1))
        ]);

    private sealed class SyncIntegrationHarness : IAsyncDisposable
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        private readonly WebApplicationFactory<Program> _apiFactory;
        private ServiceProvider _provider;
        private AsyncServiceScope _scope;
        private readonly string _directory;
        private readonly IConfiguration _configuration;
        private readonly TestUploadConnectivity _connectivity;

        private SyncIntegrationHarness(
            WebApplicationFactory<Program> apiFactory,
            ServiceProvider provider,
            AsyncServiceScope scope,
            string directory,
            IConfiguration configuration,
            TestUploadConnectivity connectivity)
        {
            _apiFactory = apiFactory;
            _provider = provider;
            _scope = scope;
            _directory = directory;
            _configuration = configuration;
            _connectivity = connectivity;
        }

        public ISyncQueueRepository SyncQueueRepository =>
            _scope.ServiceProvider.GetRequiredService<ISyncQueueRepository>();

        public OrderSyncService SyncService =>
            _scope.ServiceProvider.GetRequiredService<OrderSyncService>();

        public bool IsOnline
        {
            get => _connectivity.IsOnline;
            set => _connectivity.IsOnline = value;
        }

        public static async Task<SyncIntegrationHarness> CreateAsync()
        {
            var directory = Path.Combine(Path.GetTempPath(), "RetailPOS.Api.Tests", Guid.NewGuid().ToString("N"));
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["LocalDatabase:DatabasePath"] = Path.Combine(directory, "retail-pos.db")
                })
                .Build();
            var apiFactory = new WebApplicationFactory<Program>();
            var connectivity = new TestUploadConnectivity();
            var provider = BuildProvider(configuration, apiFactory, connectivity);
            var scope = provider.CreateAsyncScope();
            var harness = new SyncIntegrationHarness(
                apiFactory, provider, scope, directory, configuration, connectivity);
            await harness._scope.ServiceProvider.GetRequiredService<LocalDatabaseInitializer>().InitializeAsync();
            return harness;
        }

        public Task EnqueueAsync(
            OrderUploadPayload payload,
            Guid? queueId = null)
        {
            var item = new SyncQueueRecord(
                queueId ?? Guid.Parse("90000000-0000-0000-0000-000000000001"),
                "Order",
                payload.LocalOrderId,
                JsonSerializer.Serialize(payload, JsonOptions),
                payload.IdempotencyKey,
                SyncQueueStatus.Pending,
                0,
                NowUtc,
                null,
                NowUtc,
                NowUtc);

            return SyncQueueRepository.EnqueueAsync(item);
        }

        public async Task RestartAsync()
        {
            await _scope.DisposeAsync();
            await _provider.DisposeAsync();
            SqliteConnection.ClearAllPools();
            _provider = BuildProvider(_configuration, _apiFactory, _connectivity);
            _scope = _provider.CreateAsyncScope();
            await _scope.ServiceProvider.GetRequiredService<LocalDatabaseInitializer>().InitializeAsync();
        }

        private static ServiceProvider BuildProvider(
            IConfiguration configuration,
            WebApplicationFactory<Program> apiFactory,
            TestUploadConnectivity connectivity)
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddLocalPersistence(configuration);
            services.AddSingleton<IOrderSyncClock>(new StubOrderSyncClock(NowUtc));
            services.AddScoped<IOrderUploadClient>(_ => new SwitchableOrderUploadClient(
                connectivity,
                new HttpOrderUploadClient(apiFactory.CreateClient())));
            services.AddScoped<OrderSyncService>();
            return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        }

        public async ValueTask DisposeAsync()
        {
            await _scope.DisposeAsync();
            await _provider.DisposeAsync();
            await _apiFactory.DisposeAsync();
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, recursive: true);
            }
        }
    }

    private sealed class StubOrderSyncClock(DateTimeOffset utcNow) : IOrderSyncClock
    {
        public DateTimeOffset UtcNow => utcNow;
    }

    private sealed class TestUploadConnectivity
    {
        public bool IsOnline { get; set; } = true;
    }

    private sealed class SwitchableOrderUploadClient(
        TestUploadConnectivity connectivity,
        IOrderUploadClient onlineClient) : IOrderUploadClient
    {
        public Task<OrderUploadResult> UploadAsync(
            OrderUploadPayload payload,
            CancellationToken cancellationToken = default) =>
            connectivity.IsOnline
                ? onlineClient.UploadAsync(payload, cancellationToken)
                : throw new HttpRequestException("API offline.");
    }
}
