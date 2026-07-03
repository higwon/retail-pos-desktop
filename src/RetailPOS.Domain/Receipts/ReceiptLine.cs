using RetailPOS.Domain.Common;

namespace RetailPOS.Domain.Receipts;

public sealed class ReceiptLine
{
    public ReceiptLine(string productName, decimal unitPrice, int quantity, decimal discountAmount = 0m)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be greater than zero.");
        }

        ProductName = DomainGuard.Required(productName, nameof(productName));
        UnitPrice = DomainGuard.Money(unitPrice, nameof(unitPrice));
        Quantity = quantity;
        var grossAmount = UnitPrice * Quantity;
        DiscountAmount = DomainGuard.Money(discountAmount, nameof(discountAmount));
        if (DiscountAmount > grossAmount)
        {
            throw new ArgumentOutOfRangeException(nameof(discountAmount), "Discount cannot exceed the gross line amount.");
        }
    }

    public string ProductName { get; }
    public decimal UnitPrice { get; }
    public int Quantity { get; }
    public decimal GrossAmount => UnitPrice * Quantity;
    public decimal DiscountAmount { get; }
    public decimal TotalAmount => GrossAmount - DiscountAmount;
}
