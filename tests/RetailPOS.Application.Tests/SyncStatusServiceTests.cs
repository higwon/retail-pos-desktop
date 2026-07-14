using RetailPOS.Application.Persistence;
using RetailPOS.Application.Sync;

namespace RetailPOS.Application.Tests;

public sealed class SyncStatusServiceTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 7, 14, 1, 2, 3, TimeSpan.Zero);

    [Fact]
    public async Task GetSnapshotAsync_SeparatesReviewRequiredAndResolvedCounts()
    {
        var service = new SyncStatusService(
            new StubSyncQueueRepository(
                QueueItem(SyncQueueStatus.Exhausted),
                QueueItem(SyncQueueStatus.Resolved),
                QueueItem(SyncQueueStatus.Completed)),
            new StubClock());

        var snapshot = await service.GetSnapshotAsync(50);

        Assert.Equal(1, snapshot.ReviewRequiredCount);
        Assert.Equal(1, snapshot.ResolvedCount);
        Assert.Equal(1, snapshot.CompletedCount);
    }

    private static SyncQueueRecord QueueItem(SyncQueueStatus status) => new(
        Guid.NewGuid(), "Order", Guid.NewGuid(), "{}", Guid.NewGuid().ToString("N"),
        status, status == SyncQueueStatus.Exhausted ? 5 : 0,
        Now, null, Now, Now);

    private sealed class StubClock : IOrderSyncClock
    {
        public DateTimeOffset UtcNow => Now;
    }

    private sealed class StubSyncQueueRepository(params SyncQueueRecord[] items)
        : ISyncQueueRepository
    {
        public Task<IReadOnlyList<SyncQueueRecord>> GetRecentAsync(
            int count, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<SyncQueueRecord>>(items.Take(count).ToArray());

        public Task EnqueueAsync(SyncQueueRecord item, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<SyncQueueRecord>> GetDuePendingAsync(
            DateTimeOffset asOfUtc, int count, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> ExistsByReferenceKeyAsync(
            string referenceKey, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task UpdateRetryAsync(
            Guid id, int retryCount, DateTimeOffset nextAttemptAtUtc,
            string? lastErrorSummary, DateTimeOffset updatedAtUtc,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task MarkCompletedAsync(
            Guid id, DateTimeOffset completedAtUtc, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task MarkResolvedAsync(
            Guid id, DateTimeOffset resolvedAtUtc, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task MarkExhaustedAsync(
            Guid id, int retryCount, string? lastErrorSummary,
            DateTimeOffset exhaustedAtUtc, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
