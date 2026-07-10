using RetailPOS.Application.Checkout;
using RetailPOS.Application.Persistence;
using RetailPOS.Application.Receipts;
using RetailPOS.Desktop.ViewModels;
using RetailPOS.Domain.Payments;
using RetailPOS.Domain.Products;

namespace RetailPOS.Desktop.Tests;

public sealed class PaymentDialogViewModelTests
{
    private static readonly DateTimeOffset ApprovedAtUtc =
        new(2026, 7, 7, 1, 2, 3, TimeSpan.Zero);

    [Fact]
    public async Task CardPayment_UsesCurrentCheckoutAndCompletesOrder()
    {
        var session = SessionWithProduct();
        var payment = new StubPaymentStartService(Approved(PaymentMethod.Card));
        var completion = new StubOrderCompletionService();
        var receipts = new StubReceiptService();
        var display = new CheckoutDisplayState();
        var viewModel = ViewModel(session, payment, completion, receipts, display);

        await viewModel.ApproveCardPaymentCommand.ExecuteAsync(null);

        Assert.Equal(PaymentMethod.Card, payment.StartedWithMethod);
        Assert.Equal(3600m, payment.StartedWithCart?.Total);
        Assert.Equal(payment.Result.PendingCheckoutId, completion.CompletedPendingCheckoutId);
        Assert.Equal(PaymentStatus.Approved, viewModel.Status);
        Assert.Equal(0m, viewModel.AmountDue);
        Assert.Equal(CheckoutDisplayPhase.Completed, display.Snapshot.Phase);
        Assert.NotNull(receipts.GeneratedForOrderId);
        Assert.False(viewModel.ApproveCardPaymentCommand.CanExecute(null));
        Assert.False(viewModel.ApproveCashPaymentCommand.CanExecute(null));
    }

    [Fact]
    public async Task CashPayment_UsesCashMethod()
    {
        var session = SessionWithProduct();
        var payment = new StubPaymentStartService(Approved(PaymentMethod.Cash));
        var viewModel = ViewModel(session, payment);

        await viewModel.ApproveCashPaymentCommand.ExecuteAsync(null);

        Assert.Equal(PaymentMethod.Cash, payment.StartedWithMethod);
        Assert.Equal(PaymentMethod.Cash, viewModel.Method);
    }

    [Fact]
    public async Task UnknownPayment_KeepsCartAndDoesNotCompleteOrder()
    {
        var session = SessionWithProduct();
        var payment = new StubPaymentStartService(Unknown());
        var completion = new StubOrderCompletionService();
        var display = new CheckoutDisplayState();
        var viewModel = ViewModel(session, payment, completion, displayState: display);

        await viewModel.ApproveCardPaymentCommand.ExecuteAsync(null);

        Assert.True(viewModel.IsUnknown);
        Assert.Equal(3600m, viewModel.AmountDue);
        Assert.Null(completion.CompletedPendingCheckoutId);
        Assert.Equal(CheckoutDisplayPhase.PaymentFailed, display.Snapshot.Phase);
        Assert.Contains("unknown", viewModel.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PaymentInProgress_DisablesBothMethods()
    {
        var session = SessionWithProduct();
        var payment = new BlockingPaymentStartService();
        var viewModel = ViewModel(session, payment);

        var execution = viewModel.ApproveCardPaymentCommand.ExecuteAsync(null);
        await payment.Started.Task;

        Assert.True(viewModel.IsPaymentInProgress);
        Assert.False(viewModel.ApproveCardPaymentCommand.CanExecute(null));
        Assert.False(viewModel.ApproveCashPaymentCommand.CanExecute(null));

        payment.Complete(Approved(PaymentMethod.Card));
        await execution;
        Assert.False(viewModel.IsPaymentInProgress);
    }

    [Fact]
    public async Task Dispose_CancelsRequestAndIgnoresLateResult()
    {
        var session = SessionWithProduct();
        var payment = new BlockingPaymentStartService();
        var display = new CheckoutDisplayState();
        var viewModel = ViewModel(session, payment, displayState: display);
        var execution = viewModel.ApproveCardPaymentCommand.ExecuteAsync(null);
        await payment.Started.Task;

        viewModel.Dispose();
        await payment.Cancelled.Task;
        payment.Complete(Approved(PaymentMethod.Card));
        await execution;

        Assert.Null(viewModel.Status);
        Assert.Equal(3600m, viewModel.AmountDue);
        Assert.Equal(CheckoutDisplayPhase.PaymentWaiting, display.Snapshot.Phase);
        Assert.False(viewModel.ApproveCardPaymentCommand.CanExecute(null));
    }

    [Fact]
    public void Commands_TrackCheckoutAndDisposeLifecycle()
    {
        var session = new CheckoutSession();
        var viewModel = ViewModel(session, new StubPaymentStartService(Approved(PaymentMethod.Card)));

        Assert.False(viewModel.ApproveCardPaymentCommand.CanExecute(null));
        Assert.False(viewModel.ApproveCashPaymentCommand.CanExecute(null));

        session.AddProduct(Product());
        Assert.True(viewModel.ApproveCardPaymentCommand.CanExecute(null));
        Assert.True(viewModel.ApproveCashPaymentCommand.CanExecute(null));

        viewModel.Dispose();
        Assert.False(viewModel.ApproveCardPaymentCommand.CanExecute(null));
        Assert.False(viewModel.ApproveCashPaymentCommand.CanExecute(null));
    }

    [Fact]
    public async Task ReceiptFailure_KeepsApprovedPaymentAndClearsCart()
    {
        var session = SessionWithProduct();
        var receipts = new StubReceiptService(throwOnGenerate: true);
        var viewModel = ViewModel(
            session,
            new StubPaymentStartService(Approved(PaymentMethod.Card)),
            receipts: receipts);

        await viewModel.ApproveCardPaymentCommand.ExecuteAsync(null);

        Assert.Equal(PaymentStatus.Approved, viewModel.Status);
        Assert.Equal(0m, viewModel.AmountDue);
        Assert.Contains("Receipt preview", viewModel.Message);
    }

    private static PaymentDialogViewModel ViewModel(
        CheckoutSession session,
        IRecoverablePaymentStartService payment,
        IOrderCompletionService? completion = null,
        IReceiptService? receipts = null,
        CheckoutDisplayState? displayState = null) => new(
            session,
            payment,
            completion ?? new StubOrderCompletionService(),
            receipts ?? new StubReceiptService(),
            new ReceiptPreviewState(),
            displayState ?? new CheckoutDisplayState());

    private static CheckoutSession SessionWithProduct()
    {
        var session = new CheckoutSession();
        session.AddProduct(Product());
        return session;
    }

    private static Product Product() => new(
        Guid.NewGuid(), "SKU-Cola", Guid.NewGuid().ToString("N"), "Cola", "Beverages", 3600m);

    private static RecoverablePaymentStartResult Approved(PaymentMethod method) => new(
        Guid.NewGuid(), Guid.NewGuid(), PendingCheckoutStatus.ApprovedButOrderNotCreated,
        PaymentStatus.Approved, method, 3600m, 3600m, "APP-1", "TERM-1", ApprovedAtUtc, null);

    private static RecoverablePaymentStartResult Unknown() => new(
        Guid.NewGuid(), null, PendingCheckoutStatus.ManagerReviewRequired,
        PaymentStatus.Unknown, PaymentMethod.Card, 3600m, null, null, null, null,
        "Payment approval status is unknown and requires review.");

    private sealed class StubPaymentStartService(RecoverablePaymentStartResult result)
        : IRecoverablePaymentStartService
    {
        public RecoverablePaymentStartResult Result { get; } = result;
        public CartSnapshot? StartedWithCart { get; private set; }
        public PaymentMethod? StartedWithMethod { get; private set; }

        public Task<RecoverablePaymentStartResult> StartAsync(
            CartSnapshot cart,
            PaymentMethod method,
            CancellationToken cancellationToken = default)
        {
            StartedWithCart = cart;
            StartedWithMethod = method;
            return Task.FromResult(Result);
        }
    }

    private sealed class BlockingPaymentStartService : IRecoverablePaymentStartService
    {
        private readonly TaskCompletionSource<RecoverablePaymentStartResult> _completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Cancelled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<RecoverablePaymentStartResult> StartAsync(
            CartSnapshot cart,
            PaymentMethod method,
            CancellationToken cancellationToken = default)
        {
            Started.SetResult();
            using var registration = cancellationToken.Register(() => Cancelled.TrySetResult());
            return await _completion.Task;
        }

        public void Complete(RecoverablePaymentStartResult result) => _completion.TrySetResult(result);
    }

    private sealed class StubOrderCompletionService : IOrderCompletionService
    {
        public Guid? CompletedPendingCheckoutId { get; private set; }

        public Task<OrderCompletionResult> CompleteAsync(Guid pendingCheckoutId, CancellationToken cancellationToken = default)
        {
            CompletedPendingCheckoutId = pendingCheckoutId;
            return Task.FromResult(new OrderCompletionResult(Guid.NewGuid(), AlreadyCompleted: false));
        }
    }

    private sealed class StubReceiptService(bool throwOnGenerate = false) : IReceiptService
    {
        public Guid? GeneratedForOrderId { get; private set; }

        public Task<ReceiptPreview> GenerateAsync(Guid localOrderId, CancellationToken cancellationToken = default)
        {
            if (throwOnGenerate)
            {
                throw new InvalidOperationException("Receipt generation failed.");
            }

            GeneratedForOrderId = localOrderId;
            return Task.FromResult(new ReceiptPreview(
                "Store", "Terminal", "Order", "Cashier", "Register", ApprovedAtUtc,
                new DateOnly(2026, 7, 7), [], [], 3600m, 0m, 3600m, "receipt"));
        }
    }
}
