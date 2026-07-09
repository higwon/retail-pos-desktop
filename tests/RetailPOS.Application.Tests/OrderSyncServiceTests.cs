using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using RetailPOS.Application.Orders;
using RetailPOS.Application.Persistence;
using RetailPOS.Application.Sync;

namespace RetailPOS.Application.Tests;

public sealed class OrderSyncServiceTests
{
    private static readonly DateTimeOffset AsOfUtc = new(2026, 7, 9, 1, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset CompletedAtUtc = new(2026, 7, 9, 1, 0, 5, TimeSpan.Zero);
    private static readonly Guid QueueItemId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");

    [Fact]
    public async Task ProcessDueAsync_UploadsDueOrderAndMarksCompleted()
    {
        var queue = new RecordingSyncQueueRepository(QueueItem());
        var client = new RecordingOrderUploadClient();
        var service = Service(queue, client);

        var result = await service.ProcessDueAsync(AsOfUtc, 10);

        Assert.Equal(1, result.ProcessedCount);
        Assert.Equal(1, result.CompletedCount);
        Assert.Equal(0, result.RetriedCount);
        Assert.Equal(QueueItemId, Assert.Single(queue.CompletedIds));
        Assert.Equal("POS-20260709-000001", Assert.Single(client.Payloads).LocalOrderNumber);
    }

    [Fact]
    public async Task ProcessDueAsync_IdempotentDuplicateSuccessMarksCompleted()
    {
        var queue = new RecordingSyncQueueRepository(QueueItem());
        var client = new RecordingOrderUploadClient(
            new OrderUploadResult(
                Guid.Parse("dddddddd-0000-0000-0000-000000000001"),
                "SYNCED-POS-20260709-000001",
                "Synced"));
        var service = Service(queue, client);

        var result = await service.ProcessDueAsync(AsOfUtc, 10);

        Assert.Equal(1, result.CompletedCount);
        Assert.Equal(QueueItemId, Assert.Single(queue.CompletedIds));
    }

    [Fact]
    public async Task ProcessDueAsync_TransientFailureSchedulesRetry()
    {
        var queue = new RecordingSyncQueueRepository(QueueItem());
        var client = new RecordingOrderUploadClient(exception: new HttpRequestException("offline"));
        var service = Service(queue, client);

        var result = await service.ProcessDueAsync(AsOfUtc, 10);

        Assert.Equal(0, result.CompletedCount);
        Assert.Equal(1, result.RetriedCount);
        var retry = Assert.Single(queue.Retries);
        Assert.Equal(QueueItemId, retry.Id);
        Assert.Equal(1, retry.RetryCount);
        Assert.Equal(AsOfUtc.AddMinutes(1), retry.NextAttemptAtUtc);
        Assert.Equal("offline", retry.LastErrorSummary);
    }

    [Fact]
    public async Task ProcessDueAsync_FifthFailureMarksExhaustedForReview()
    {
        var queue = new RecordingSyncQueueRepository(QueueItem(retryCount: 4));
        var client = new RecordingOrderUploadClient(exception: new HttpRequestException("timeout"));
        var service = Service(queue, client);

        var result = await service.ProcessDueAsync(AsOfUtc, 10);

        Assert.Equal(0, result.RetriedCount);
        Assert.Equal(1, result.ExhaustedCount);
        var exhausted = Assert.Single(queue.Exhausted);
        Assert.Equal(OrderSyncService.MaxAutomaticAttempts, exhausted.RetryCount);
        Assert.Equal("timeout", exhausted.LastErrorSummary);
        Assert.Empty(queue.CompletedIds);
        Assert.Empty(queue.Retries);
    }

    [Fact]
    public async Task ProcessDueAsync_MaxAttemptItemIsMarkedExhaustedForReview()
    {
        var queue = new RecordingSyncQueueRepository(QueueItem(retryCount: 5));
        var client = new RecordingOrderUploadClient();
        var service = Service(queue, client);

        var result = await service.ProcessDueAsync(AsOfUtc, 10);

        Assert.Equal(1, result.ExhaustedCount);
        Assert.Empty(client.Payloads);
        Assert.Empty(queue.CompletedIds);
        Assert.Empty(queue.Retries);
        Assert.Equal(QueueItemId, Assert.Single(queue.Exhausted).Id);
    }

    [Fact]
    public async Task ProcessDueAsync_NonOrderItemIsSkippedWithoutStatusChange()
    {
        var queue = new RecordingSyncQueueRepository(QueueItem(itemType: "Product"));
        var client = new RecordingOrderUploadClient();
        var service = Service(queue, client);

        var result = await service.ProcessDueAsync(AsOfUtc, 10);

        Assert.Equal(1, result.SkippedCount);
        Assert.Equal(0, result.ExhaustedCount);
        Assert.Empty(client.Payloads);
        Assert.Empty(queue.CompletedIds);
        Assert.Empty(queue.Retries);
        Assert.Empty(queue.Exhausted);
    }

    [Fact]
    public async Task ProcessDueAsync_IdempotencyConflictMarksExhaustedForReview()
    {
        var queue = new RecordingSyncQueueRepository(QueueItem());
        var client = new RecordingOrderUploadClient(
            exception: new OrderUploadConflictException("idempotency conflict"));
        var service = Service(queue, client);

        var result = await service.ProcessDueAsync(AsOfUtc, 10);

        Assert.Equal(0, result.RetriedCount);
        Assert.Equal(1, result.ExhaustedCount);
        Assert.Empty(queue.Retries);
        var exhausted = Assert.Single(queue.Exhausted);
        Assert.Equal(0, exhausted.RetryCount);
        Assert.Equal("idempotency conflict", exhausted.LastErrorSummary);
    }

    [Fact]
    public async Task ProcessDueAsync_UploadFailureLogsSafeStructuredContext()
    {
        var queue = new RecordingSyncQueueRepository(QueueItem());
        var client = new RecordingOrderUploadClient(
            exception: new HttpRequestException("payment gateway password=secret"));
        var logger = new RecordingLogger<OrderSyncService>();
        var service = Service(queue, client, logger);

        await service.ProcessDueAsync(AsOfUtc, 10);

        Assert.Contains(logger.Messages, message => message.Contains("scheduled for retry", StringComparison.OrdinalIgnoreCase));
        Assert.All(logger.Messages, message =>
        {
            Assert.DoesNotContain("password", message, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("secret", message, StringComparison.OrdinalIgnoreCase);
        });
    }

    private static OrderSyncService Service(
        ISyncQueueRepository queue,
        IOrderUploadClient client,
        ILogger<OrderSyncService>? logger = null) =>
        new(queue, client, new StubOrderSyncClock(CompletedAtUtc), logger ?? NullLogger<OrderSyncService>.Instance);

    private static SyncQueueRecord QueueItem(int retryCount = 0, string itemType = "Order") => new(
        QueueItemId,
        itemType,
        Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001"),
        JsonSerializer.Serialize(Payload(), new JsonSerializerOptions(JsonSerializerDefaults.Web)),
        "10000000000000000000000000000001:20000000000000000000000000000001:bbbbbbbb000000000000000000000001",
        SyncQueueStatus.Pending,
        retryCount,
        AsOfUtc,
        null,
        AsOfUtc.AddMinutes(-5),
        AsOfUtc.AddMinutes(-5));

    private static OrderUploadPayload Payload() => new(
        OrderUploadPayload.CurrentSchemaVersion,
        Guid.Parse("10000000-0000-0000-0000-000000000001"),
        Guid.Parse("20000000-0000-0000-0000-000000000001"),
        Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001"),
        "10000000000000000000000000000001:20000000000000000000000000000001:bbbbbbbb000000000000000000000001",
        "POS-20260709-000001",
        new DateOnly(2026, 7, 9),
        Guid.Parse("30000000-0000-0000-0000-000000000001"),
        3600m,
        200m,
        3400m,
        new DateTimeOffset(2026, 7, 9, 0, 58, 0, TimeSpan.Zero),
        [
            new OrderUploadLinePayload(
                Guid.Parse("11111111-0000-0000-0000-000000000001"),
                "Cola",
                1800m,
                2,
                200m,
                3400m)
        ],
        [
            new OrderUploadPaymentPayload(
                "Card",
                3400m,
                "APP-001",
                "TX-001",
                new DateTimeOffset(2026, 7, 9, 0, 59, 0, TimeSpan.Zero))
        ]);

    private sealed class RecordingOrderUploadClient(
        OrderUploadResult? response = null,
        Exception? exception = null) : IOrderUploadClient
    {
        public List<OrderUploadPayload> Payloads { get; } = [];

        public Task<OrderUploadResult> UploadAsync(
            OrderUploadPayload payload,
            CancellationToken cancellationToken = default)
        {
            if (exception is not null)
            {
                throw exception;
            }

            Payloads.Add(payload);
            return Task.FromResult(response ?? new OrderUploadResult(
                Guid.Parse("cccccccc-0000-0000-0000-000000000001"),
                "SYNCED-POS-20260709-000001",
                "Synced"));
        }
    }

    private sealed class RecordingSyncQueueRepository(params SyncQueueRecord[] items) : ISyncQueueRepository
    {
        public List<Guid> CompletedIds { get; } = [];
        public List<RetryUpdate> Retries { get; } = [];
        public List<ExhaustedUpdate> Exhausted { get; } = [];

        public Task EnqueueAsync(SyncQueueRecord item, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<SyncQueueRecord>> GetDuePendingAsync(
            DateTimeOffset asOfUtc,
            int count,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<SyncQueueRecord>>(items.Take(count).ToArray());

        public Task<IReadOnlyList<SyncQueueRecord>> GetRecentAsync(
            int count,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<SyncQueueRecord>>(items.Take(count).ToArray());

        public Task<bool> ExistsByReferenceKeyAsync(
            string referenceKey,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task UpdateRetryAsync(
            Guid id,
            int retryCount,
            DateTimeOffset nextAttemptAtUtc,
            string? lastErrorSummary,
            DateTimeOffset updatedAtUtc,
            CancellationToken cancellationToken = default)
        {
            Retries.Add(new RetryUpdate(id, retryCount, nextAttemptAtUtc, lastErrorSummary, updatedAtUtc));
            return Task.CompletedTask;
        }

        public Task MarkCompletedAsync(
            Guid id,
            DateTimeOffset completedAtUtc,
            CancellationToken cancellationToken = default)
        {
            CompletedIds.Add(id);
            return Task.CompletedTask;
        }

        public Task MarkResolvedAsync(
            Guid id,
            DateTimeOffset resolvedAtUtc,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task MarkExhaustedAsync(
            Guid id,
            int retryCount,
            string? lastErrorSummary,
            DateTimeOffset exhaustedAtUtc,
            CancellationToken cancellationToken = default)
        {
            Exhausted.Add(new ExhaustedUpdate(id, retryCount, lastErrorSummary, exhaustedAtUtc));
            return Task.CompletedTask;
        }
    }

    private sealed class StubOrderSyncClock(DateTimeOffset utcNow) : IOrderSyncClock
    {
        public DateTimeOffset UtcNow => utcNow;
    }

    private sealed record RetryUpdate(
        Guid Id,
        int RetryCount,
        DateTimeOffset NextAttemptAtUtc,
        string? LastErrorSummary,
        DateTimeOffset UpdatedAtUtc);

    private sealed record ExhaustedUpdate(
        Guid Id,
        int RetryCount,
        string? LastErrorSummary,
        DateTimeOffset ExhaustedAtUtc);

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception));
        }
    }
}
