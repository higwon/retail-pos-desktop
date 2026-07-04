namespace RetailPOS.Infrastructure.Persistence.Entities;

public sealed class PaymentEntity
{
    public Guid Id { get; set; }
    public Guid LocalOrderId { get; set; }
    public int SortOrder { get; set; }
    public int Method { get; set; }
    public int Status { get; set; }
    public decimal RequestedAmount { get; set; }
    public decimal? ApprovedAmount { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }
    public string? ApprovalCode { get; set; }
    public string? TransactionReference { get; set; }
    public string? FailureReason { get; set; }
    public OrderEntity Order { get; set; } = null!;
}
