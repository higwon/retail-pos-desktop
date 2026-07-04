namespace RetailPOS.Infrastructure.Persistence.Entities;

public sealed class OrderLineEntity
{
    public Guid Id { get; set; }
    public Guid LocalOrderId { get; set; }
    public int SortOrder { get; set; }
    public Guid ProductId { get; set; }
    public required string ProductNameSnapshot { get; set; }
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public decimal GrossAmount { get; set; }
    public decimal LineDiscountAmount { get; set; }
    public decimal LineTotalAmount { get; set; }
    public OrderEntity Order { get; set; } = null!;
}
