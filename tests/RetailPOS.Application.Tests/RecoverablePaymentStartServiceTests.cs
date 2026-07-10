using RetailPOS.Application.Checkout;
using RetailPOS.Application.Payments;
using RetailPOS.Application.Persistence;
using RetailPOS.Domain.Payments;
using RetailPOS.Domain.Products;

namespace RetailPOS.Application.Tests;

public sealed class RecoverablePaymentStartServiceTests
{
    private static readonly Guid PendingCheckoutId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid OrderId = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001");
    private static readonly Guid StoreId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid TerminalId = Guid.Parse("20000000-0000-0000-0000-000000000001");
    private static readonly Guid CashierId = Guid.Parse("30000000-0000-0000-0000-000000000001");
    private static readonly DateTimeOffset CreatedAtUtc = new(2026, 7, 7, 1, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset ApprovedAtUtc = new(2026, 7, 7, 1, 0, 5, TimeSpan.Zero);

    [Fact]
    public async Task StartAsync_SavesPendingAttemptBeforeCallingTerminal()
    {
        var repository = new RecordingPendingCheckoutRepository();
        var terminal = new StubPaymentTerminal(Approved(), request =>
        {
            Assert.Single(repository.Saved);
            Assert.Equal(PendingCheckoutStatus.AwaitingPayment, repository.Saved[0].RecoveryStatus);
            Assert.Equal(request.PaymentAttemptId, repository.Saved[0].Id);
        });

        var result = await Service(repository, terminal).StartAsync(Cart(), PaymentMethod.Card);

        Assert.Equal(PendingCheckoutId, terminal.Request?.PaymentAttemptId);
        Assert.Equal(PendingCheckoutId, result.PendingCheckoutId);
        Assert.Equal(OrderId, result.OrderId);
        Assert.Equal(PendingCheckoutStatus.ApprovedButOrderNotCreated, repository.Saved[1].RecoveryStatus);
        Assert.Equal(PaymentStatus.Approved, repository.Saved[1].PaymentStatus);
    }

    [Fact]
    public async Task StartAsync_FailedCardPaymentDoesNotCreateOrder()
    {
        var repository = new RecordingPendingCheckoutRepository();
        var result = await Service(repository, new StubPaymentTerminal(Failed()))
            .StartAsync(Cart(), PaymentMethod.Card);

        Assert.False(result.IsApproved);
        Assert.Null(result.OrderId);
        Assert.Equal(PendingCheckoutStatus.PaymentFailed, repository.Saved[1].RecoveryStatus);
        Assert.Equal(PaymentStatus.Failed, repository.Saved[1].PaymentStatus);
    }

    [Fact]
    public async Task StartAsync_UnknownCardOutcomeRequiresManagerReview()
    {
        var repository = new RecordingPendingCheckoutRepository();
        var result = await Service(repository, new StubPaymentTerminal(Unknown()))
            .StartAsync(Cart(), PaymentMethod.Card);

        Assert.True(result.IsUnknown);
        Assert.Null(result.OrderId);
        Assert.Equal(PendingCheckoutStatus.ManagerReviewRequired, repository.Saved[1].RecoveryStatus);
        Assert.Equal(PaymentStatus.Unknown, repository.Saved[1].PaymentStatus);
    }

    [Fact]
    public async Task StartAsync_UnsupportedTerminalResultFailsClosed()
    {
        var repository = new RecordingPendingCheckoutRepository();
        var unsupported = Approved() with { Status = PaymentStatus.Pending };

        var result = await Service(repository, new StubPaymentTerminal(unsupported))
            .StartAsync(Cart(), PaymentMethod.Card);

        Assert.True(result.IsUnknown);
        Assert.Equal(PendingCheckoutStatus.ManagerReviewRequired, result.RecoveryStatus);
    }

    [Fact]
    public async Task StartAsync_CashBypassesPaymentTerminal()
    {
        var repository = new RecordingPendingCheckoutRepository();
        var terminal = new StubPaymentTerminal(Approved());
        var cash = new StubCashPaymentProcessor(Approved());

        var result = await Service(repository, terminal, cash)
            .StartAsync(Cart(), PaymentMethod.Cash);

        Assert.Null(terminal.Request);
        Assert.Equal(PendingCheckoutId, cash.Request?.PaymentAttemptId);
        Assert.True(result.IsApproved);
    }

    [Fact]
    public async Task StartAsync_CancellationAfterDispatchPersistsUnknownOutcome()
    {
        var repository = new RecordingPendingCheckoutRepository();
        var terminal = new CancellingPaymentTerminal();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var result = await Service(repository, terminal)
            .StartAsync(Cart(), PaymentMethod.Card, cancellation.Token);

        Assert.False(terminal.WasCalled);
        Assert.Equal(PaymentStatus.Cancelled, result.PaymentStatus);
        Assert.Equal(PendingCheckoutStatus.PaymentFailed, result.RecoveryStatus);

        using var dispatchedCancellation = new CancellationTokenSource();
        var secondRepository = new RecordingPendingCheckoutRepository();
        var secondTerminal = new CancellingPaymentTerminal();
        var task = Service(secondRepository, secondTerminal)
            .StartAsync(Cart(), PaymentMethod.Card, dispatchedCancellation.Token);
        await secondTerminal.Started.Task;
        dispatchedCancellation.Cancel();

        var dispatchedResult = await task;

        Assert.True(dispatchedResult.IsUnknown);
        Assert.Equal(PendingCheckoutStatus.ManagerReviewRequired, secondRepository.Saved[1].RecoveryStatus);
    }

    [Fact]
    public async Task StartAsync_RejectsOverlappingAttempts()
    {
        var repository = new RecordingPendingCheckoutRepository();
        var terminal = new BlockingPaymentTerminal();
        var service = Service(repository, terminal);
        var first = service.StartAsync(Cart(), PaymentMethod.Card);
        await terminal.Started.Task;

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.StartAsync(Cart(), PaymentMethod.Card));

        terminal.Complete(Approved());
        await first;
        Assert.Equal(2, repository.Saved.Count);
    }

    [Fact]
    public async Task StartAsync_RejectsNewAttemptWhileTerminalHasUnresolvedPayment()
    {
        var repository = new RecordingPendingCheckoutRepository();
        repository.Saved.Add(Pending(PendingCheckoutStatus.ManagerReviewRequired));
        var terminal = new StubPaymentTerminal(Approved());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Service(repository, terminal).StartAsync(Cart(), PaymentMethod.Card));

        Assert.Null(terminal.Request);
    }

    private static RecoverablePaymentStartService Service(
        IPendingCheckoutRepository repository,
        IPaymentTerminal terminal,
        ICashPaymentProcessor? cash = null) => new(
            repository,
            terminal,
            cash ?? new StubCashPaymentProcessor(Approved()),
            new StubCheckoutContextProvider(),
            new StubCheckoutClock(CreatedAtUtc),
            new SequenceCheckoutIdGenerator(PendingCheckoutId, OrderId));

    private static CartSnapshot Cart()
    {
        var session = new CheckoutSession();
        session.AddProduct(new Product(
            Guid.NewGuid(), "SKU-Cola", Guid.NewGuid().ToString("N"), "Cola", "Beverages", 3600m));
        return session.Snapshot;
    }

    private static PaymentAuthorizationResult Approved() => new(
        PaymentStatus.Approved, 3600m, 3600m, "APP-1", "TERM-1", ApprovedAtUtc, null);

    private static PaymentAuthorizationResult Failed() => new(
        PaymentStatus.Failed, 3600m, null, null, null, null, "Payment was declined.");

    private static PaymentAuthorizationResult Unknown() => new(
        PaymentStatus.Unknown, 3600m, null, null, null, null, "Approval status is unknown.");

    private static PendingCheckoutRecord Pending(PendingCheckoutStatus status) => new(
        Guid.NewGuid(), StoreId, TerminalId, CashierId, CreatedAtUtc, status,
        "{}", "{}", PaymentStatus.Unknown, null, null, null, null, null, null, CreatedAtUtc);

    private sealed class StubPaymentTerminal(
        PaymentAuthorizationResult result,
        Action<PaymentAuthorizationRequest>? onRequest = null) : IPaymentTerminal
    {
        public PaymentAuthorizationRequest? Request { get; private set; }

        public Task<PaymentAuthorizationResult> AuthorizeAsync(
            PaymentAuthorizationRequest request,
            CancellationToken cancellationToken = default)
        {
            Request = request;
            onRequest?.Invoke(request);
            return Task.FromResult(result);
        }
    }

    private sealed class StubCashPaymentProcessor(PaymentAuthorizationResult result) : ICashPaymentProcessor
    {
        public CashPaymentRequest? Request { get; private set; }

        public Task<PaymentAuthorizationResult> AcceptAsync(
            CashPaymentRequest request,
            CancellationToken cancellationToken = default)
        {
            Request = request;
            return Task.FromResult(result);
        }
    }

    private sealed class CancellingPaymentTerminal : IPaymentTerminal
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool WasCalled { get; private set; }

        public async Task<PaymentAuthorizationResult> AuthorizeAsync(
            PaymentAuthorizationRequest request,
            CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            Started.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("Unreachable");
        }
    }

    private sealed class BlockingPaymentTerminal : IPaymentTerminal
    {
        private readonly TaskCompletionSource<PaymentAuthorizationResult> _completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<PaymentAuthorizationResult> AuthorizeAsync(
            PaymentAuthorizationRequest request,
            CancellationToken cancellationToken = default)
        {
            Started.SetResult();
            return _completion.Task;
        }

        public void Complete(PaymentAuthorizationResult result) => _completion.SetResult(result);
    }

    private sealed class RecordingPendingCheckoutRepository : IPendingCheckoutRepository
    {
        public List<PendingCheckoutRecord> Saved { get; } = [];

        public Task SaveAsync(PendingCheckoutRecord checkout, CancellationToken cancellationToken = default)
        {
            Saved.Add(checkout);
            return Task.CompletedTask;
        }

        public Task<PendingCheckoutRecord?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Saved.LastOrDefault(checkout => checkout.Id == id));

        public Task<IReadOnlyList<PendingCheckoutRecord>> GetUnresolvedAsync(CancellationToken cancellationToken = default) =>
            cancellationToken.IsCancellationRequested
                ? Task.FromCanceled<IReadOnlyList<PendingCheckoutRecord>>(cancellationToken)
                : Task.FromResult<IReadOnlyList<PendingCheckoutRecord>>(Saved.ToArray());

        public Task MarkCompletedAsync(Guid id, Guid orderId, DateTimeOffset completedAtUtc, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task MarkManagerReviewRequiredAsync(Guid id, DateTimeOffset updatedAtUtc, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class StubCheckoutContextProvider : ICheckoutContextProvider
    {
        public CheckoutContext GetCurrent() => new(StoreId, TerminalId, CashierId);
    }

    private sealed class StubCheckoutClock(DateTimeOffset utcNow) : ICheckoutClock
    {
        public DateTimeOffset UtcNow => utcNow;
    }

    private sealed class SequenceCheckoutIdGenerator(params Guid[] ids) : ICheckoutIdGenerator
    {
        private int _index;
        public Guid NewId() => ids[_index++];
    }
}
