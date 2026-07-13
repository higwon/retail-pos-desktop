using RetailPOS.Domain.Carts;
using RetailPOS.Domain.Discounts;
using RetailPOS.Domain.Products;

namespace RetailPOS.Application.Checkout;

public sealed class CheckoutSession
{
    private readonly Cart _cart = new();

    public event EventHandler? Changed;

    public CartSnapshot Snapshot => new(
        _cart.Items.Select(item => new CartLineSnapshot(
            item.ProductId,
            item.ProductName,
            item.UnitPrice,
            item.Quantity,
            item.LineTotal)).ToArray(),
        _cart.Subtotal,
        _cart.Discount?.Type,
        _cart.Discount?.Value,
        _cart.DiscountAmount,
        _cart.Total);

    public void AddProduct(Product product, int quantity = 1)
    {
        _cart.Add(product, quantity);
        NotifyChanged();
    }

    public void IncreaseQuantity(Guid productId)
    {
        var item = FindItem(productId);
        _cart.UpdateQuantity(productId, checked(item.Quantity + 1));
        NotifyChanged();
    }

    public void DecreaseQuantity(Guid productId)
    {
        var item = FindItem(productId);
        if (item.Quantity == 1)
        {
            _cart.Remove(productId);
        }
        else
        {
            _cart.UpdateQuantity(productId, item.Quantity - 1);
        }

        NotifyChanged();
    }

    public void RemoveProduct(Guid productId)
    {
        if (_cart.Remove(productId))
        {
            NotifyChanged();
        }
    }

    public void ApplyFixedDiscount(decimal amount)
    {
        _cart.ApplyDiscount(ManualDiscount.FixedAmount(amount));
        NotifyChanged();
    }

    public void ApplyPercentageDiscount(decimal rate)
    {
        _cart.ApplyDiscount(ManualDiscount.Percentage(rate));
        NotifyChanged();
    }

    public void ClearDiscount()
    {
        if (_cart.Discount is null)
        {
            return;
        }

        _cart.ApplyDiscount(null);
        NotifyChanged();
    }

    public void Clear()
    {
        if (_cart.Items.Count == 0 && _cart.Discount is null)
        {
            return;
        }

        _cart.Clear();
        NotifyChanged();
    }

    private CartItem FindItem(Guid productId) =>
        _cart.Items.SingleOrDefault(item => item.ProductId == productId)
        ?? throw new KeyNotFoundException($"Product '{productId}' is not in the current checkout.");

    private void NotifyChanged() => Changed?.Invoke(this, EventArgs.Empty);
}

public sealed record CartSnapshot(
    IReadOnlyList<CartLineSnapshot> Lines,
    decimal Subtotal,
    DiscountType? DiscountType,
    decimal? DiscountValue,
    decimal DiscountAmount,
    decimal Total)
{
    public int ItemCount => Lines.Sum(line => line.Quantity);
    public bool IsEmpty => Lines.Count == 0;
}

public sealed record CartLineSnapshot(
    Guid ProductId,
    string ProductName,
    decimal UnitPrice,
    int Quantity,
    decimal LineTotal);
