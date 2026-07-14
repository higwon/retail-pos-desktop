namespace RetailPOS.Infrastructure.Persistence.Entities;

public sealed class PendingCheckoutEntity
{
    public Guid Id { get; set; }
    public Guid StoreId { get; set; }
    public Guid TerminalId { get; set; }
    public Guid CashierId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public int RecoveryStatus { get; set; }
    public required string CartSnapshotJson { get; set; }
    public required string PaymentSnapshotJson { get; set; }
    public int PaymentStatus { get; set; }
    public string? ApprovalCode { get; set; }
    public decimal? ApprovedAmount { get; set; }
    public decimal? CashTenderedAmount { get; set; }
    public decimal? ChangeAmount { get; set; }
    public string? TransactionReference { get; set; }
    public DateTime? PaymentApprovedAtUtc { get; set; }
    public Guid? OrderId { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public DateTime LastUpdatedAtUtc { get; set; }
}
