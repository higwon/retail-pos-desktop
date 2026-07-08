using RetailPOS.Application.Checkout;
using RetailPOS.Desktop.ViewModels;
using RetailPOS.Domain.Products;

namespace RetailPOS.Desktop.Tests;

public sealed class CustomerDisplayViewModelTests
{
    [Fact]
    public void Constructor_ReflectsCurrentSharedCartState()
    {
        var session = new CheckoutSession();
        var product = Product("Cola", 1800m);
        session.AddProduct(product);
        session.AddProduct(product);
        session.ApplyFixedDiscount(200m);

        var viewModel = new CustomerDisplayViewModel(session, new CheckoutDisplayState());

        var line = Assert.Single(viewModel.Lines);
        Assert.Equal("Cola", line.ProductName);
        Assert.Equal("Qty 2", line.QuantityText);
        Assert.Equal("3,600 KRW", line.LineTotalText);
        Assert.Equal(2, viewModel.ItemCount);
        Assert.Equal(200m, viewModel.DiscountAmount);
        Assert.True(viewModel.HasDiscount);
        Assert.Equal("3,400 KRW", viewModel.TotalAmount);
        Assert.Equal("Please check your items", viewModel.StatusMessage);
    }

    [Fact]
    public void CheckoutChanges_UpdateDisplayCartState()
    {
        var session = new CheckoutSession();
        var viewModel = new CustomerDisplayViewModel(session, new CheckoutDisplayState());

        session.AddProduct(Product("Water", 1000m));

        Assert.False(viewModel.IsEmpty);
        Assert.Equal("1 items", viewModel.ItemSummary);
        Assert.Equal("1,000 KRW", viewModel.TotalAmount);
    }

    [Fact]
    public void PaymentAndCompletionState_IsSynchronizedAndPreservedForNewViewModel()
    {
        var session = new CheckoutSession();
        var displayState = new CheckoutDisplayState();
        var viewModel = new CustomerDisplayViewModel(session, displayState);

        displayState.ShowPaymentWaiting(RetailPOS.Domain.Payments.PaymentMethod.Card, 1000m);

        Assert.Equal("Payment in progress", viewModel.StatusMessage);
        Assert.Equal("Card payment waiting", viewModel.PaymentMessage);

        viewModel.Dispose();
        displayState.ShowCompleted();
        var reopened = new CustomerDisplayViewModel(session, displayState);

        Assert.Equal("Thank you", reopened.StatusMessage);
        Assert.Equal("Payment complete. Please take your receipt.", reopened.PaymentMessage);
    }

    [Fact]
    public void Dispose_UnsubscribesFromSharedState()
    {
        var session = new CheckoutSession();
        var displayState = new CheckoutDisplayState();
        var viewModel = new CustomerDisplayViewModel(session, displayState);

        viewModel.Dispose();
        session.AddProduct(Product("Water", 1000m));
        displayState.ShowPaymentFailed("Declined");

        Assert.True(viewModel.IsEmpty);
        Assert.Equal("Please check your items", viewModel.StatusMessage);
    }

    private static Product Product(string name, decimal price) => new(
        Guid.NewGuid(), $"SKU-{name}", Guid.NewGuid().ToString("N"), name, "Beverages", price);
}
