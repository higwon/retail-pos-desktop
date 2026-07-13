using RetailPOS.Domain.Common;
using RetailPOS.Domain.Products;

namespace RetailPOS.Domain.Carts;

public sealed class CartItem
{
    internal CartItem(Product product, int quantity)
    {
        ProductId = product.Id;
        ProductName = product.Name;
        CategoryName = product.CategoryName;
        UnitPrice = product.UnitPrice;
        SetQuantity(quantity);
    }

    public Guid ProductId { get; }
    public string ProductName { get; }
    public string CategoryName { get; }
    public decimal UnitPrice { get; }
    public int Quantity { get; private set; }
    public decimal LineTotal => UnitPrice * Quantity;

    internal void SetQuantity(int quantity)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be greater than zero.");
        }

        Quantity = quantity;
    }

    internal void IncreaseQuantity(int quantity)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be greater than zero.");
        }

        Quantity = checked(Quantity + quantity);
    }
}
