using RetailPOS.Domain.Common;

namespace RetailPOS.Domain.Orders;

public sealed class OrderLine
{
    public OrderLine(Guid productId, string productNameSnapshot, decimal unitPrice,
        int quantity, decimal lineDiscountAmount = 0m)
    {
        if (productId == Guid.Empty)
        {
            throw new ArgumentException("Product identity is required.", nameof(productId));
        }

        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be greater than zero.");
        }

        ProductId = productId;
        ProductNameSnapshot = DomainGuard.Required(productNameSnapshot, nameof(productNameSnapshot));
        UnitPrice = DomainGuard.Money(unitPrice, nameof(unitPrice));
        Quantity = quantity;
        var grossAmount = UnitPrice * Quantity;
        LineDiscountAmount = DomainGuard.Money(lineDiscountAmount, nameof(lineDiscountAmount));
        if (LineDiscountAmount > grossAmount)
        {
            throw new ArgumentOutOfRangeException(nameof(lineDiscountAmount), "Line discount cannot exceed the gross line amount.");
        }
    }

    public Guid ProductId { get; }
    public string ProductNameSnapshot { get; }
    public decimal UnitPrice { get; }
    public int Quantity { get; }
    public decimal GrossAmount => UnitPrice * Quantity;
    public decimal LineDiscountAmount { get; }
    public decimal LineTotalAmount => GrossAmount - LineDiscountAmount;
}
