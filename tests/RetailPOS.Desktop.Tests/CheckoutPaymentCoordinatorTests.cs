using RetailPOS.Application.Checkout;
using RetailPOS.Application.Persistence;
using RetailPOS.Application.Receipts;
using RetailPOS.Desktop.ViewModels;
using RetailPOS.Desktop.Workflow;
using RetailPOS.Domain.Payments;
using RetailPOS.Domain.Products;

namespace RetailPOS.Desktop.Tests;

public sealed class CheckoutPaymentCoordinatorTests
{
    private static readonly DateTimeOffset ApprovedAtUtc =
        new(2026, 7, 13, 1, 2, 3, TimeSpan.Zero);

    [Fact]
    public async Task ApprovedPayment_CompletesOrderGeneratesReceiptAndClearsCart()
    {
        var session = SessionWithProduct();
        var completion = new RecordingOrderCompletionService();
        var receipts = new StubReceiptService();
        var receiptState = new ReceiptPreviewState();
        var display = new CheckoutDisplayState();
        var paymentStart = new StubPaymentStartService(ApprovedCash());
        var coordinator = new CheckoutPaymentCoordinator(
            session,
            paymentStart,
            completion,
            receipts,
            receiptState,
            display);

        var execution = await coordinator.ExecuteCashAsync(5000m);

        Assert.True(execution.Payment.IsApproved);
        Assert.Equal(5000m, execution.Payment.CashTenderedAmount);
        Assert.Equal(1400m, execution.Payment.ChangeAmount);
        Assert.Equal(5000m, paymentStart.CashTenderedAmount);
        Assert.NotNull(completion.CompletedPendingCheckoutId);
        Assert.NotNull(receipts.GeneratedForOrderId);
        Assert.True(receiptState.HasReceipt);
        Assert.True(session.Snapshot.IsEmpty);
        Assert.Equal(CheckoutDisplayPhase.Completed, display.Snapshot.Phase);
    }

    [Fact]
    public async Task FailedPayment_KeepsCartAndDoesNotCompleteOrder()
    {
        var session = SessionWithProduct();
        var completion = new RecordingOrderCompletionService();
        var display = new CheckoutDisplayState();
        var coordinator = new CheckoutPaymentCoordinator(
            session,
            new StubPaymentStartService(Failed()),
            completion,
            new StubReceiptService(),
            new ReceiptPreviewState(),
            display);

        var execution = await coordinator.ExecuteAsync(PaymentMethod.Card);

        Assert.False(execution.Payment.IsApproved);
        Assert.Null(completion.CompletedPendingCheckoutId);
        Assert.False(session.Snapshot.IsEmpty);
        Assert.Equal(CheckoutDisplayPhase.PaymentFailed, display.Snapshot.Phase);
    }

    [Fact]
    public async Task ReceiptFailure_PreservesApprovedCompletionAndReturnsSafeMessage()
    {
        var session = SessionWithProduct();
        var receiptState = new ReceiptPreviewState();
        receiptState.Set(Receipt(Guid.NewGuid()));
        var coordinator = new CheckoutPaymentCoordinator(
            session,
            new StubPaymentStartService(Approved(PaymentMethod.Card)),
            new RecordingOrderCompletionService(),
            new StubReceiptService(throwOnGenerate: true),
            receiptState,
            new CheckoutDisplayState());

        var execution = await coordinator.ExecuteAsync(PaymentMethod.Card);

        Assert.True(execution.Payment.IsApproved);
        Assert.True(session.Snapshot.IsEmpty);
        Assert.False(receiptState.HasReceipt);
        Assert.Contains("Receipt preview", execution.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CancellationAfterOrderCompletion_PreservesApprovedCompletion()
    {
        using var cancellation = new CancellationTokenSource();
        var session = SessionWithProduct();
        var receipts = new StubReceiptService();
        var display = new CheckoutDisplayState();
        var coordinator = new CheckoutPaymentCoordinator(
            session,
            new StubPaymentStartService(Approved(PaymentMethod.Card)),
            new RecordingOrderCompletionService(cancellation.Cancel),
            receipts,
            new ReceiptPreviewState(),
            display);

        var execution = await coordinator.ExecuteAsync(
            PaymentMethod.Card,
            cancellation.Token);

        Assert.True(execution.Payment.IsApproved);
        Assert.True(session.Snapshot.IsEmpty);
        Assert.Equal(CheckoutDisplayPhase.Completed, display.Snapshot.Phase);
        Assert.NotNull(receipts.GeneratedForOrderId);
        Assert.False(receipts.ReceivedCancellationToken.CanBeCanceled);
    }

    [Fact]
    public async Task ConcurrentPayment_IsRejectedBySharedBusyGate()
    {
        var session = SessionWithProduct();
        var payment = new BlockingPaymentStartService();
        var display = new CheckoutDisplayState();
        var coordinator = new CheckoutPaymentCoordinator(
            session,
            payment,
            new RecordingOrderCompletionService(),
            new StubReceiptService(),
            new ReceiptPreviewState(),
            display);
        var first = coordinator.ExecuteAsync(PaymentMethod.Card);
        await payment.Started.Task;

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            coordinator.ExecuteAsync(PaymentMethod.Cash));

        payment.Complete(Failed());
        await first;
    }

    [Fact]
    public async Task CancelActivePayment_CancelsTheInFlightTerminalRequest()
    {
        var session = SessionWithProduct();
        var payment = new BlockingPaymentStartService();
        var display = new CheckoutDisplayState();
        var coordinator = new CheckoutPaymentCoordinator(
            session,
            payment,
            new RecordingOrderCompletionService(),
            new StubReceiptService(),
            new ReceiptPreviewState(),
            display);
        var execution = coordinator.ExecuteAsync(PaymentMethod.Card);
        await payment.Started.Task;

        coordinator.CancelActivePayment();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => execution);
        Assert.False(session.Snapshot.IsEmpty);
        Assert.Equal(CheckoutDisplayPhase.PaymentFailed, display.Snapshot.Phase);
    }

    private static CheckoutSession SessionWithProduct()
    {
        var session = new CheckoutSession();
        session.AddProduct(new Product(
            Guid.NewGuid(), "SKU-Cola", "8801000000011", "Cola", "Beverages", 3600m));
        return session;
    }

    private static RecoverablePaymentStartResult Approved(PaymentMethod method) => new(
        Guid.NewGuid(), Guid.NewGuid(), PendingCheckoutStatus.ApprovedButOrderNotCreated,
        PaymentStatus.Approved, method, 3600m, 3600m,
        "APP-1", "TX-1", ApprovedAtUtc, null);

    private static RecoverablePaymentStartResult ApprovedCash() => new(
        Guid.NewGuid(), Guid.NewGuid(), PendingCheckoutStatus.ApprovedButOrderNotCreated,
        PaymentStatus.Approved, PaymentMethod.Cash, 3600m, 3600m,
        "APP-CASH", "CASH-1", ApprovedAtUtc, null, 5000m, 1400m);

    private static RecoverablePaymentStartResult Failed() => new(
        Guid.NewGuid(), null, PendingCheckoutStatus.PaymentFailed,
        PaymentStatus.Failed, PaymentMethod.Card, 3600m, null,
        null, null, null, "Card was declined.");

    private static ReceiptPreview Receipt(Guid localOrderId) => new(
        "Store", "Address", "Previous order", "Cashier", "Register", ApprovedAtUtc,
        new DateOnly(2026, 7, 13), [], [], 3600m, 0m, 3600m, "receipt", localOrderId);

    private sealed class StubPaymentStartService(RecoverablePaymentStartResult result)
        : IRecoverablePaymentStartService
    {
        public decimal? CashTenderedAmount { get; private set; }

        public Task<RecoverablePaymentStartResult> StartAsync(
            CartSnapshot cart,
            PaymentMethod method,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(result);

        public Task<RecoverablePaymentStartResult> StartCashAsync(
            CartSnapshot cart,
            decimal tenderedAmount,
            CancellationToken cancellationToken = default)
        {
            CashTenderedAmount = tenderedAmount;
            return Task.FromResult(result);
        }
    }

    private sealed class BlockingPaymentStartService : IRecoverablePaymentStartService
    {
        private readonly TaskCompletionSource<RecoverablePaymentStartResult> _completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Started { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<RecoverablePaymentStartResult> StartAsync(
            CartSnapshot cart,
            PaymentMethod method,
            CancellationToken cancellationToken = default)
        {
            Started.TrySetResult();
            return await _completion.Task.WaitAsync(cancellationToken);
        }

        public void Complete(RecoverablePaymentStartResult result) =>
            _completion.TrySetResult(result);
    }

    private sealed class RecordingOrderCompletionService(Action? onCompleted = null)
        : IOrderCompletionService
    {
        public Guid? CompletedPendingCheckoutId { get; private set; }

        public Task<OrderCompletionResult> CompleteAsync(
            Guid pendingCheckoutId,
            CancellationToken cancellationToken = default)
        {
            CompletedPendingCheckoutId = pendingCheckoutId;
            onCompleted?.Invoke();
            return Task.FromResult(new OrderCompletionResult(Guid.NewGuid(), AlreadyCompleted: false));
        }
    }

    private sealed class StubReceiptService(bool throwOnGenerate = false) : IReceiptService
    {
        public Guid? GeneratedForOrderId { get; private set; }
        public CancellationToken ReceivedCancellationToken { get; private set; }

        public Task<ReceiptPreview> GenerateAsync(
            Guid localOrderId,
            CancellationToken cancellationToken = default)
        {
            ReceivedCancellationToken = cancellationToken;
            if (throwOnGenerate)
            {
                throw new InvalidOperationException("Receipt failure.");
            }

            GeneratedForOrderId = localOrderId;
            return Task.FromResult(new ReceiptPreview(
                "Store", "Address", "Order", "Cashier", "Register", ApprovedAtUtc,
                new DateOnly(2026, 7, 13), [], [], 3600m, 0m, 3600m, "receipt"));
        }
    }
}
