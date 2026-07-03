using RetailPOS.Domain.Carts;
using RetailPOS.Domain.Discounts;
using RetailPOS.Domain.Products;

namespace RetailPOS.Domain.Tests;

public sealed class CartTests
{
    [Fact]
    public void Add_MergesSameProductAndCalculatesTotals()
    {
        var product = Product(5_000m);
        var cart = new Cart();

        cart.Add(product, 2);
        cart.Add(product);

        Assert.Single(cart.Items);
        Assert.Equal(3, cart.Items[0].Quantity);
        Assert.Equal(15_000m, cart.Subtotal);
        Assert.Equal(15_000m, cart.Total);
    }

    [Fact]
    public void UpdateAndRemove_ChangeCartContents()
    {
        var product = Product(2_000m);
        var cart = new Cart();
        cart.Add(product);

        cart.UpdateQuantity(product.Id, 4);
        var removed = cart.Remove(product.Id);

        Assert.True(removed);
        Assert.Empty(cart.Items);
    }

    [Fact]
    public void Add_RejectsInactiveProduct()
    {
        var product = new Product(Guid.NewGuid(), "SKU", "BAR", "Name", "Category", 1_000m, false);

        Assert.Throws<InvalidOperationException>(() => new Cart().Add(product));
    }

    [Fact]
    public void ApplyDiscount_NeverMakesTotalNegative()
    {
        var cart = new Cart();
        cart.Add(Product(10_000m));

        cart.ApplyDiscount(ManualDiscount.FixedAmount(50_000m));

        Assert.Equal(10_000m, cart.DiscountAmount);
        Assert.Equal(0m, cart.Total);
    }

    private static Product Product(decimal unitPrice) =>
        new(Guid.NewGuid(), $"SKU-{Guid.NewGuid():N}", $"BAR-{Guid.NewGuid():N}", "Product", "Category", unitPrice);
}
