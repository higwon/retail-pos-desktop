namespace RetailPOS.Application.Persistence;

public sealed record SyncQueueRecord(
    Guid Id,
    string ItemType,
    Guid AggregateId,
    string? PayloadJson,
    string? ReferenceKey,
    SyncQueueStatus Status,
    int RetryCount,
    DateTimeOffset NextAttemptAtUtc,
    string? LastErrorSummary,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public enum SyncQueueStatus
{
    Pending,
    Completed,
    Resolved,
    Exhausted
}
