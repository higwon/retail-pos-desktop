namespace RetailPOS.Infrastructure.Persistence.Entities;

public sealed class OrderEntity
{
    public Guid LocalOrderId { get; set; }
    public required string LocalOrderNumber { get; set; }
    public Guid StoreId { get; set; }
    public Guid TerminalId { get; set; }
    public Guid CashierId { get; set; }
    public DateOnly BusinessDate { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public int Status { get; set; }
    public decimal SubtotalAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public List<OrderLineEntity> Lines { get; set; } = [];
    public List<PaymentEntity> Payments { get; set; } = [];
}
