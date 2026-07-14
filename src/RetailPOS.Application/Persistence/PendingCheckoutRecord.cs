using RetailPOS.Domain.Payments;

namespace RetailPOS.Application.Persistence;

public sealed record PendingCheckoutRecord(
    Guid Id,
    Guid StoreId,
    Guid TerminalId,
    Guid CashierId,
    DateTimeOffset CreatedAtUtc,
    PendingCheckoutStatus RecoveryStatus,
    string CartSnapshotJson,
    string PaymentSnapshotJson,
    PaymentStatus PaymentStatus,
    string? ApprovalCode,
    decimal? ApprovedAmount,
    string? TransactionReference,
    DateTimeOffset? PaymentApprovedAtUtc,
    Guid? OrderId,
    DateTimeOffset? CompletedAtUtc,
    DateTimeOffset LastUpdatedAtUtc,
    decimal? CashTenderedAmount = null,
    decimal? ChangeAmount = null);

public enum PendingCheckoutStatus
{
    AwaitingPayment,
    PaymentFailed,
    ApprovedButOrderNotCreated,
    ManagerReviewRequired,
    Completed,
    ReviewResolved
}
