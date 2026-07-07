using RetailPOS.Application.Checkout;
using RetailPOS.Domain.Discounts;
using RetailPOS.Domain.Products;

namespace RetailPOS.Application.Tests;

public sealed class CheckoutSessionTests
{
    [Fact]
    public void CartOperations_ProduceConsistentSnapshotsAndNotifications()
    {
        var session = new CheckoutSession();
        var product = Product("Cola", 1800m);
        var notifications = 0;
        session.Changed += (_, _) => notifications++;

        session.AddProduct(product);
        session.AddProduct(product);
        session.DecreaseQuantity(product.Id);

        var snapshot = session.Snapshot;
        Assert.Equal(1, snapshot.ItemCount);
        Assert.Equal(1800m, snapshot.Total);
        Assert.Equal(3, notifications);

        session.DecreaseQuantity(product.Id);
        Assert.True(session.Snapshot.IsEmpty);
        Assert.Equal(4, notifications);
    }

    [Fact]
    public void RemoveAndClear_NotifyOnlyWhenStateChanges()
    {
        var session = new CheckoutSession();
        var first = Product("Water", 1000m);
        var second = Product("Cola", 1800m);
        var notifications = 0;
        session.Changed += (_, _) => notifications++;

        session.AddProduct(first);
        session.AddProduct(second);
        session.RemoveProduct(first.Id);
        session.Clear();
        session.Clear();
        session.RemoveProduct(Guid.NewGuid());

        Assert.True(session.Snapshot.IsEmpty);
        Assert.Equal(4, notifications);
    }

    [Fact]
    public void ManualDiscounts_UpdateSnapshotAndCanBeCleared()
    {
        var session = new CheckoutSession();
        session.AddProduct(Product("Cola", 2000m));

        session.ApplyFixedDiscount(500m);

        Assert.Equal(DiscountType.FixedAmount, session.Snapshot.DiscountType);
        Assert.Equal(500m, session.Snapshot.DiscountValue);
        Assert.Equal(1500m, session.Snapshot.Total);

        session.ApplyPercentageDiscount(25m);

        Assert.Equal(DiscountType.Percentage, session.Snapshot.DiscountType);
        Assert.Equal(500m, session.Snapshot.DiscountAmount);

        session.ClearDiscount();

        Assert.Null(session.Snapshot.DiscountType);
        Assert.Equal(2000m, session.Snapshot.Total);
    }

    private static Product Product(string name, decimal price) => new(
        Guid.NewGuid(),
        $"SKU-{name}",
        Guid.NewGuid().ToString("N"),
        name,
        "Beverages",
        price);
}
