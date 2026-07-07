using RetailPOS.Application.Checkout;
using RetailPOS.Application.Payments;
using RetailPOS.Desktop.ViewModels;
using RetailPOS.Domain.Payments;
using RetailPOS.Domain.Products;

namespace RetailPOS.Desktop.Tests;

public sealed class PaymentDialogViewModelTests
{
    private static readonly DateTimeOffset ApprovedAtUtc =
        new(2026, 7, 7, 1, 2, 3, TimeSpan.Zero);

    [Fact]
    public async Task ApproveCardPayment_UsesCurrentCheckoutTotal()
    {
        var session = new CheckoutSession();
        session.AddProduct(Product("Cola", 1800m));
        session.AddProduct(Product("Cola", 1800m));
        var viewModel = new PaymentDialogViewModel(
            session,
            new LocalPaymentSimulator(() => ApprovedAtUtc));

        await viewModel.ApproveCardPaymentCommand.ExecuteAsync(null);

        Assert.Equal(3600m, viewModel.AmountDue);
        Assert.Equal("3,600 KRW", viewModel.TotalAmount);
        Assert.Equal(PaymentStatus.Approved, viewModel.Status);
        Assert.Equal(PaymentMethod.Card, viewModel.Method);
        Assert.Equal(3600m, viewModel.ApprovedAmount);
        Assert.Equal("APP-CARD-000000003600", viewModel.ApprovalCode);
        Assert.Equal(ApprovedAtUtc, viewModel.ApprovedAtUtc);
    }

    [Fact]
    public async Task FailPayment_ReturnsUserSafeFailureState()
    {
        var session = new CheckoutSession();
        session.AddProduct(Product("Water", 1000m));
        var viewModel = new PaymentDialogViewModel(
            session,
            new LocalPaymentSimulator(() => ApprovedAtUtc));

        await viewModel.FailPaymentCommand.ExecuteAsync(null);

        Assert.Equal(PaymentStatus.Failed, viewModel.Status);
        Assert.True(viewModel.IsFailed);
        Assert.False(viewModel.IsApproved);
        Assert.Null(viewModel.ApprovalCode);
        Assert.Equal("Payment was declined by the local simulator.", viewModel.Message);
    }

    [Fact]
    public void Commands_AreDisabledUntilCheckoutHasPositiveTotal()
    {
        var session = new CheckoutSession();
        var viewModel = new PaymentDialogViewModel(
            session,
            new LocalPaymentSimulator(() => ApprovedAtUtc));

        Assert.False(viewModel.ApproveCardPaymentCommand.CanExecute(null));
        Assert.False(viewModel.ApproveCashPaymentCommand.CanExecute(null));
        Assert.False(viewModel.FailPaymentCommand.CanExecute(null));

        session.AddProduct(Product("Water", 1000m));

        Assert.True(viewModel.ApproveCardPaymentCommand.CanExecute(null));
        Assert.True(viewModel.ApproveCashPaymentCommand.CanExecute(null));
        Assert.True(viewModel.FailPaymentCommand.CanExecute(null));
    }

    private static Product Product(string name, decimal price) => new(
        Guid.NewGuid(), $"SKU-{name}", Guid.NewGuid().ToString("N"), name, "Beverages", price);
}
