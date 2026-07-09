namespace RetailPOS.Application.Persistence;

public interface ISyncQueueRepository
{
    Task EnqueueAsync(SyncQueueRecord item, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SyncQueueRecord>> GetDuePendingAsync(
        DateTimeOffset asOfUtc,
        int count,
        CancellationToken cancellationToken = default);
    Task<bool> ExistsByReferenceKeyAsync(
        string referenceKey,
        CancellationToken cancellationToken = default);
    Task UpdateRetryAsync(
        Guid id,
        int retryCount,
        DateTimeOffset nextAttemptAtUtc,
        string? lastErrorSummary,
        DateTimeOffset updatedAtUtc,
        CancellationToken cancellationToken = default);
    Task MarkCompletedAsync(
        Guid id,
        DateTimeOffset completedAtUtc,
        CancellationToken cancellationToken = default);
    Task MarkResolvedAsync(
        Guid id,
        DateTimeOffset resolvedAtUtc,
        CancellationToken cancellationToken = default);
    Task MarkExhaustedAsync(
        Guid id,
        int retryCount,
        string? lastErrorSummary,
        DateTimeOffset exhaustedAtUtc,
        CancellationToken cancellationToken = default);
}
