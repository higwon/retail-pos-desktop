using RetailPOS.Application.Checkout;
using RetailPOS.Application.Payments;
using RetailPOS.Application.Persistence;
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
        var service = new StubPaymentStartService(Approved());
        var completion = new StubOrderCompletionService();
        var viewModel = new PaymentDialogViewModel(session, service, completion);

        await viewModel.ApproveCardPaymentCommand.ExecuteAsync(null);

        Assert.Equal(0m, viewModel.AmountDue);
        Assert.Equal("0 KRW", viewModel.TotalAmount);
        Assert.Equal(3600m, service.StartedWithCart?.Total);
        Assert.Equal(PaymentMethod.Card, service.StartedWithMethod);
        Assert.Equal(PaymentSimulationMode.Approve, service.StartedWithMode);
        Assert.Equal(service.Result.PendingCheckoutId, completion.CompletedPendingCheckoutId);
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
        var service = new StubPaymentStartService(Failed());
        var completion = new StubOrderCompletionService();
        var viewModel = new PaymentDialogViewModel(session, service, completion);

        await viewModel.FailPaymentCommand.ExecuteAsync(null);

        Assert.Equal(PaymentSimulationMode.Fail, service.StartedWithMode);
        Assert.Null(completion.CompletedPendingCheckoutId);
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
            new StubPaymentStartService(Approved()),
            new StubOrderCompletionService());

        Assert.False(viewModel.ApproveCardPaymentCommand.CanExecute(null));
        Assert.False(viewModel.ApproveCashPaymentCommand.CanExecute(null));
        Assert.False(viewModel.FailPaymentCommand.CanExecute(null));

        session.AddProduct(Product("Water", 1000m));

        Assert.True(viewModel.ApproveCardPaymentCommand.CanExecute(null));
        Assert.True(viewModel.ApproveCashPaymentCommand.CanExecute(null));
        Assert.True(viewModel.FailPaymentCommand.CanExecute(null));
    }

    [Fact]
    public void Dispose_UnsubscribesFromCheckoutSessionChanges()
    {
        var session = new CheckoutSession();
        var viewModel = new PaymentDialogViewModel(
            session,
            new StubPaymentStartService(Approved()),
            new StubOrderCompletionService());

        viewModel.Dispose();
        session.AddProduct(Product("Water", 1000m));

        Assert.Equal(0m, viewModel.AmountDue);
        Assert.Equal("0 KRW", viewModel.TotalAmount);
        Assert.False(viewModel.ApproveCardPaymentCommand.CanExecute(null));
    }

    [Fact]
    public async Task CompletionFailure_KeepsCartAndShowsUserSafeMessage()
    {
        var session = new CheckoutSession();
        session.AddProduct(Product("Water", 1000m));
        var viewModel = new PaymentDialogViewModel(
            session,
            new StubPaymentStartService(Approved()),
            new StubOrderCompletionService(throwOnComplete: true));

        await viewModel.ApproveCardPaymentCommand.ExecuteAsync(null);

        Assert.Equal(1000m, viewModel.AmountDue);
        Assert.Equal(PaymentStatus.Failed, viewModel.Status);
        Assert.Equal("Payment could not be completed. Keep the cart and try again or ask a manager to review checkout status.",
            viewModel.Message);
    }

    private static RecoverablePaymentStartResult Approved() => new(
        Guid.NewGuid(),
        Guid.NewGuid(),
        PendingCheckoutStatus.ApprovedButOrderNotCreated,
        PaymentStatus.Approved,
        PaymentMethod.Card,
        3600m,
        3600m,
        "APP-CARD-000000003600",
        "SIM-CARD-20260707010203-000000003600",
        ApprovedAtUtc,
        null);

    private static RecoverablePaymentStartResult Failed() => new(
        Guid.NewGuid(),
        null,
        PendingCheckoutStatus.PaymentFailed,
        PaymentStatus.Failed,
        PaymentMethod.Card,
        1000m,
        null,
        null,
        null,
        null,
        "Payment was declined by the local simulator.");

    private static Product Product(string name, decimal price) => new(
        Guid.NewGuid(), $"SKU-{name}", Guid.NewGuid().ToString("N"), name, "Beverages", price);

    private sealed class StubPaymentStartService(RecoverablePaymentStartResult result) : IRecoverablePaymentStartService
    {
        public RecoverablePaymentStartResult Result { get; } = result;
        public CartSnapshot? StartedWithCart { get; private set; }
        public PaymentMethod? StartedWithMethod { get; private set; }
        public PaymentSimulationMode? StartedWithMode { get; private set; }

        public Task<RecoverablePaymentStartResult> StartAsync(
            CartSnapshot cart,
            PaymentMethod method,
            PaymentSimulationMode mode = PaymentSimulationMode.Approve,
            CancellationToken cancellationToken = default)
        {
            StartedWithCart = cart;
            StartedWithMethod = method;
            StartedWithMode = mode;
            return Task.FromResult(Result);
        }
    }

    private sealed class StubOrderCompletionService(bool throwOnComplete = false) : IOrderCompletionService
    {
        public Guid? CompletedPendingCheckoutId { get; private set; }

        public Task<OrderCompletionResult> CompleteAsync(
            Guid pendingCheckoutId,
            CancellationToken cancellationToken = default)
        {
            if (throwOnComplete)
            {
                throw new InvalidOperationException("Simulated persistence failure.");
            }

            CompletedPendingCheckoutId = pendingCheckoutId;
            return Task.FromResult(new OrderCompletionResult(Guid.NewGuid(), AlreadyCompleted: false));
        }
    }
}
