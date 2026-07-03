using RetailPOS.Domain.Discounts;
using RetailPOS.Domain.Products;

namespace RetailPOS.Domain.Carts;

public sealed class Cart
{
    private readonly List<CartItem> _items = [];

    public IReadOnlyList<CartItem> Items => _items.AsReadOnly();
    public ManualDiscount? Discount { get; private set; }
    public decimal Subtotal => _items.Sum(item => item.LineTotal);
    public decimal DiscountAmount => Discount?.CalculateAmount(Subtotal) ?? 0m;
    public decimal Total => Subtotal - DiscountAmount;

    public void Add(Product product, int quantity = 1)
    {
        ArgumentNullException.ThrowIfNull(product);
        if (!product.IsActive)
        {
            throw new InvalidOperationException("Inactive products cannot be added to a cart.");
        }

        var existingItem = _items.SingleOrDefault(item => item.ProductId == product.Id);
        if (existingItem is null)
        {
            _items.Add(new CartItem(product, quantity));
            return;
        }

        existingItem.IncreaseQuantity(quantity);
    }

    public void UpdateQuantity(Guid productId, int quantity)
    {
        var item = FindItem(productId);
        item.SetQuantity(quantity);
    }

    public bool Remove(Guid productId)
    {
        var item = _items.SingleOrDefault(candidate => candidate.ProductId == productId);
        return item is not null && _items.Remove(item);
    }

    public void ApplyDiscount(ManualDiscount? discount) => Discount = discount;

    public void Clear()
    {
        _items.Clear();
        Discount = null;
    }

    private CartItem FindItem(Guid productId) =>
        _items.SingleOrDefault(item => item.ProductId == productId)
        ?? throw new KeyNotFoundException($"Product '{productId}' is not in the cart.");
}
