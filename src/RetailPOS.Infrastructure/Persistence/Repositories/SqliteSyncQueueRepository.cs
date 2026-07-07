using Microsoft.EntityFrameworkCore;
using RetailPOS.Application.Persistence;
using RetailPOS.Infrastructure.Persistence.Entities;
using RetailPOS.Infrastructure.Persistence.Mapping;

namespace RetailPOS.Infrastructure.Persistence.Repositories;

public sealed class SqliteSyncQueueRepository(LocalPosDbContext dbContext) : ISyncQueueRepository
{
    public async Task EnqueueAsync(SyncQueueRecord item, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (item.Status != SyncQueueStatus.Pending || item.RetryCount != 0 ||
            item.NextAttemptAtUtc != item.CreatedAtUtc)
        {
            throw new ArgumentException(
                "A new queue item must be pending with no retries and immediately due.",
                nameof(item));
        }

        dbContext.SyncQueue.Add(item.ToEntity());
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SyncQueueRecord>> GetDuePendingAsync(
        DateTimeOffset asOfUtc,
        int count,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
        var asOf = UtcTime.ToStorage(asOfUtc, nameof(asOfUtc));
        var pending = (int)SyncQueueStatus.Pending;
        var entities = await dbContext.SyncQueue.AsNoTracking()
            .Where(item => item.Status == pending && item.NextAttemptAtUtc <= asOf)
            .OrderBy(item => item.NextAttemptAtUtc)
            .ThenBy(item => item.CreatedAtUtc)
            .ThenBy(item => item.Id)
            .Take(count)
            .ToListAsync(cancellationToken);
        return entities.Select(item => item.ToRecord()).ToList();
    }

    public Task<bool> ExistsByReferenceKeyAsync(
        string referenceKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(referenceKey);
        var normalizedKey = referenceKey.Trim();
        return dbContext.SyncQueue.AnyAsync(item => item.ReferenceKey == normalizedKey, cancellationToken);
    }

    public async Task UpdateRetryAsync(
        Guid id,
        int retryCount,
        DateTimeOffset nextAttemptAtUtc,
        string? lastErrorSummary,
        DateTimeOffset updatedAtUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(retryCount);
        var entity = await FindRequiredAsync(id, cancellationToken);
        entity.RetryCount = retryCount;
        entity.NextAttemptAtUtc = UtcTime.ToStorage(nextAttemptAtUtc, nameof(nextAttemptAtUtc));
        entity.LastErrorSummary = string.IsNullOrWhiteSpace(lastErrorSummary) ? null : lastErrorSummary.Trim();
        entity.UpdatedAtUtc = UtcTime.ToStorage(updatedAtUtc, nameof(updatedAtUtc));
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task MarkCompletedAsync(
        Guid id,
        DateTimeOffset completedAtUtc,
        CancellationToken cancellationToken = default) =>
        SetStatusAsync(id, SyncQueueStatus.Completed, completedAtUtc, cancellationToken);

    public Task MarkResolvedAsync(
        Guid id,
        DateTimeOffset resolvedAtUtc,
        CancellationToken cancellationToken = default) =>
        SetStatusAsync(id, SyncQueueStatus.Resolved, resolvedAtUtc, cancellationToken);

    private async Task SetStatusAsync(
        Guid id,
        SyncQueueStatus status,
        DateTimeOffset updatedAtUtc,
        CancellationToken cancellationToken)
    {
        var entity = await FindRequiredAsync(id, cancellationToken);
        entity.Status = (int)status;
        entity.UpdatedAtUtc = UtcTime.ToStorage(updatedAtUtc, nameof(updatedAtUtc));
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<SyncQueueEntity> FindRequiredAsync(
        Guid id,
        CancellationToken cancellationToken) =>
        await dbContext.SyncQueue.SingleOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException($"Sync queue item '{id}' was not found.");
}
