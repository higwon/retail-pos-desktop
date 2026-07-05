using RetailPOS.Application.Checkout;
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

    private static Product Product(string name, decimal price) => new(
        Guid.NewGuid(),
        $"SKU-{name}",
        Guid.NewGuid().ToString("N"),
        name,
        "Beverages",
        price);
}
