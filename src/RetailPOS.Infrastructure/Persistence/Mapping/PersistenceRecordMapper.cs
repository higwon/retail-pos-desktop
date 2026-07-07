using RetailPOS.Application.Persistence;
using RetailPOS.Domain.Payments;
using RetailPOS.Infrastructure.Persistence.Entities;

namespace RetailPOS.Infrastructure.Persistence.Mapping;

internal static class PersistenceRecordMapper
{
    public static PendingCheckoutEntity ToEntity(this PendingCheckoutRecord record) => new()
    {
        Id = record.Id,
        StoreId = record.StoreId,
        TerminalId = record.TerminalId,
        CashierId = record.CashierId,
        CreatedAtUtc = UtcTime.ToStorage(record.CreatedAtUtc, nameof(record.CreatedAtUtc)),
        RecoveryStatus = (int)record.RecoveryStatus,
        CartSnapshotJson = record.CartSnapshotJson,
        PaymentSnapshotJson = record.PaymentSnapshotJson,
        PaymentStatus = (int)record.PaymentStatus,
        ApprovalCode = record.ApprovalCode,
        ApprovedAmount = record.ApprovedAmount,
        TransactionReference = record.TransactionReference,
        PaymentApprovedAtUtc = UtcTime.ToStorage(record.PaymentApprovedAtUtc, nameof(record.PaymentApprovedAtUtc)),
        OrderId = record.OrderId,
        CompletedAtUtc = UtcTime.ToStorage(record.CompletedAtUtc, nameof(record.CompletedAtUtc)),
        LastUpdatedAtUtc = UtcTime.ToStorage(record.LastUpdatedAtUtc, nameof(record.LastUpdatedAtUtc))
    };

    public static PendingCheckoutRecord ToRecord(this PendingCheckoutEntity entity) => new(
        entity.Id,
        entity.StoreId,
        entity.TerminalId,
        entity.CashierId,
        UtcTime.FromStorage(entity.CreatedAtUtc),
        (PendingCheckoutStatus)entity.RecoveryStatus,
        entity.CartSnapshotJson,
        entity.PaymentSnapshotJson,
        (PaymentStatus)entity.PaymentStatus,
        entity.ApprovalCode,
        entity.ApprovedAmount,
        entity.TransactionReference,
        UtcTime.FromStorage(entity.PaymentApprovedAtUtc),
        entity.OrderId,
        UtcTime.FromStorage(entity.CompletedAtUtc),
        UtcTime.FromStorage(entity.LastUpdatedAtUtc));

    public static SyncQueueEntity ToEntity(this SyncQueueRecord record) => new()
    {
        Id = record.Id,
        ItemType = record.ItemType,
        AggregateId = record.AggregateId,
        PayloadJson = record.PayloadJson,
        ReferenceKey = record.ReferenceKey,
        Status = (int)record.Status,
        RetryCount = record.RetryCount,
        NextAttemptAtUtc = UtcTime.ToStorage(record.NextAttemptAtUtc, nameof(record.NextAttemptAtUtc)),
        LastErrorSummary = record.LastErrorSummary,
        CreatedAtUtc = UtcTime.ToStorage(record.CreatedAtUtc, nameof(record.CreatedAtUtc)),
        UpdatedAtUtc = UtcTime.ToStorage(record.UpdatedAtUtc, nameof(record.UpdatedAtUtc))
    };

    public static SyncQueueRecord ToRecord(this SyncQueueEntity entity) => new(
        entity.Id,
        entity.ItemType,
        entity.AggregateId,
        entity.PayloadJson,
        entity.ReferenceKey,
        (SyncQueueStatus)entity.Status,
        entity.RetryCount,
        UtcTime.FromStorage(entity.NextAttemptAtUtc),
        entity.LastErrorSummary,
        UtcTime.FromStorage(entity.CreatedAtUtc),
        UtcTime.FromStorage(entity.UpdatedAtUtc));
}
