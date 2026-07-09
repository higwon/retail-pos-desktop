using RetailPOS.Application.Persistence;

namespace RetailPOS.Application.Sync;

public sealed class SyncStatusService(
    ISyncQueueRepository syncQueueRepository,
    IOrderSyncClock clock)
{
    public async Task<SyncStatusSnapshot> GetSnapshotAsync(
        int count,
        CancellationToken cancellationToken = default)
    {
        if (count <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "count must be greater than zero.");
        }

        var checkedAtUtc = UtcNow();
        var records = await syncQueueRepository.GetRecentAsync(count, cancellationToken);
        var items = records.Select(record => new SyncStatusItem(
            record.Id,
            record.ItemType,
            record.AggregateId,
            record.ReferenceKey,
            record.Status,
            record.RetryCount,
            record.NextAttemptAtUtc,
            record.LastErrorSummary,
            record.CreatedAtUtc,
            record.UpdatedAtUtc)).ToArray();

        return new SyncStatusSnapshot(
            checkedAtUtc,
            items.Count(item => item.Status == SyncQueueStatus.Pending && item.RetryCount == 0),
            items.Count(item => item.Status == SyncQueueStatus.Pending && item.RetryCount > 0),
            items.Count(item => item.Status == SyncQueueStatus.Completed),
            items.Count(item => item.Status is SyncQueueStatus.Exhausted or SyncQueueStatus.Resolved),
            items);
    }

    private DateTimeOffset UtcNow()
    {
        var value = clock.UtcNow;
        if (value.Offset != TimeSpan.Zero)
        {
            throw new InvalidOperationException("Sync status clock must return UTC timestamps.");
        }

        return value;
    }
}

public sealed record SyncStatusSnapshot(
    DateTimeOffset CheckedAtUtc,
    int PendingCount,
    int RetryCount,
    int CompletedCount,
    int ReviewCount,
    IReadOnlyList<SyncStatusItem> Items);

public sealed record SyncStatusItem(
    Guid Id,
    string ItemType,
    Guid AggregateId,
    string? ReferenceKey,
    SyncQueueStatus Status,
    int RetryCount,
    DateTimeOffset NextAttemptAtUtc,
    string? LastErrorSummary,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);
