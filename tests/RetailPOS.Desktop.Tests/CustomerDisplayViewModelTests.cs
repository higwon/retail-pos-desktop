using RetailPOS.Application.Checkout;
using RetailPOS.Desktop.ViewModels;
using RetailPOS.Domain.Payments;
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
        Assert.Equal("Beverages", line.CategoryName);
        Assert.Equal("1,800 KRW", line.UnitPriceText);
        Assert.Equal("2", line.QuantityText);
        Assert.Equal("3,600 KRW", line.LineTotalText);
        Assert.Equal(2, viewModel.ItemCount);
        Assert.Equal("3,600 KRW", viewModel.SubtotalAmount);
        Assert.Equal(200m, viewModel.DiscountAmount);
        Assert.True(viewModel.HasDiscount);
        Assert.Equal("Fixed discount (200 KRW)", viewModel.DiscountLabel);
        Assert.Equal("-200 KRW", viewModel.DiscountSummary);
        Assert.Equal("3,400 KRW", viewModel.TotalAmount);
        Assert.Equal("Please check your items", viewModel.StatusMessage);
        Assert.True(viewModel.IsReviewOrderActive);
    }

    [Fact]
    public void CheckoutChanges_UpdateDisplayCartState()
    {
        var session = new CheckoutSession();
        var viewModel = new CustomerDisplayViewModel(session, new CheckoutDisplayState());

        session.AddProduct(Product("Water", 1000m));

        Assert.False(viewModel.IsEmpty);
        Assert.Equal("1 item", viewModel.ItemSummary);
        Assert.Equal("Total quantity 1", viewModel.QuantitySummary);
        Assert.Equal("1,000 KRW", viewModel.TotalAmount);
    }

    [Fact]
    public void PaymentAndCompletionState_IsSynchronizedAndPreservedForNewViewModel()
    {
        var session = new CheckoutSession();
        var displayState = new CheckoutDisplayState();
        var viewModel = new CustomerDisplayViewModel(session, displayState);

        displayState.ShowPaymentWaiting(PaymentMethod.Card, 1000m);

        Assert.Equal("Payment in progress", viewModel.StatusMessage);
        Assert.Equal("Card payment waiting", viewModel.PaymentMessage);
        Assert.Equal("Card approval", viewModel.PaymentMethodText);
        Assert.Equal("1,000 KRW", viewModel.AmountToPayText);
        Assert.True(viewModel.IsPaymentActive);
        Assert.False(viewModel.IsPaymentProblem);

        viewModel.Dispose();
        displayState.ShowCompleted();
        var reopened = new CustomerDisplayViewModel(session, displayState);

        Assert.Equal("Thank you", reopened.StatusMessage);
        Assert.Equal("Payment complete. Please take your receipt.", reopened.PaymentMessage);
        Assert.True(reopened.IsCompletedActive);
        Assert.False(reopened.IsAmountVisible);
    }

    [Fact]
    public void PercentageDiscountAndCashFailure_UseTruthfulCustomerLabels()
    {
        var session = new CheckoutSession();
        session.AddProduct(Product("Serum", 2000m));
        session.ApplyPercentageDiscount(25m);
        var displayState = new CheckoutDisplayState();
        var viewModel = new CustomerDisplayViewModel(session, displayState);

        displayState.ShowPaymentWaiting(PaymentMethod.Cash, 1500m);
        displayState.ShowPaymentFailed("Cash payment could not be recorded.");

        Assert.Equal("Discount (25%)", viewModel.DiscountLabel);
        Assert.Equal("-500 KRW", viewModel.DiscountSummary);
        Assert.Equal("Cash payment", viewModel.PaymentMethodText);
        Assert.Equal("Payment needs attention", viewModel.StatusHeading);
        Assert.True(viewModel.IsPaymentActive);
        Assert.True(viewModel.IsPaymentProblem);
        Assert.Equal("Cash payment could not be recorded.", viewModel.PaymentMessage);
    }

    [Fact]
    public void NewSaleAfterCompletion_ReturnsDisplayToOrderReview()
    {
        var session = new CheckoutSession();
        var displayState = new CheckoutDisplayState();
        var viewModel = new CustomerDisplayViewModel(session, displayState);
        displayState.ShowPaymentWaiting(PaymentMethod.Card, 1000m);
        displayState.ShowCompleted();

        session.AddProduct(Product("Water", 1000m));

        Assert.True(viewModel.IsReviewOrderActive);
        Assert.Equal("Please check your items", viewModel.StatusMessage);
        Assert.Equal("Waiting for payment", viewModel.PaymentMessage);
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
