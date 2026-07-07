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
    public async Task StartAsync_SavesAwaitingPaymentBeforeCallingSimulator()
    {
        var repository = new RecordingPendingCheckoutRepository();
        var simulator = new RecordingPaymentSimulator(repository, ApprovedResult());
        var service = Service(repository, simulator);

        await service.StartAsync(Cart(), PaymentMethod.Card);

        Assert.True(simulator.WasCalled);
        Assert.Equal(2, repository.Saved.Count);
        Assert.Equal(PendingCheckoutStatus.AwaitingPayment, repository.Saved[0].RecoveryStatus);
        Assert.Equal(PaymentStatus.Pending, repository.Saved[0].PaymentStatus);
        Assert.Null(repository.Saved[0].OrderId);
        Assert.Equal(PendingCheckoutStatus.ApprovedButOrderNotCreated, repository.Saved[1].RecoveryStatus);
    }

    [Fact]
    public async Task StartAsync_ApprovedPaymentUpdatesSameRecordForRecovery()
    {
        var repository = new RecordingPendingCheckoutRepository();
        var service = Service(repository, new RecordingPaymentSimulator(repository, ApprovedResult()));

        var result = await service.StartAsync(Cart(), PaymentMethod.Card);
        var approved = repository.Saved[1];

        Assert.True(result.IsApproved);
        Assert.Equal(PendingCheckoutId, result.PendingCheckoutId);
        Assert.Equal(OrderId, result.OrderId);
        Assert.Equal(PendingCheckoutId, approved.Id);
        Assert.Equal(StoreId, approved.StoreId);
        Assert.Equal(TerminalId, approved.TerminalId);
        Assert.Equal(CashierId, approved.CashierId);
        Assert.Equal(PendingCheckoutStatus.ApprovedButOrderNotCreated, approved.RecoveryStatus);
        Assert.Equal(PaymentStatus.Approved, approved.PaymentStatus);
        Assert.Equal(3600m, approved.ApprovedAmount);
        Assert.Equal("APP-CARD-000000003600", approved.ApprovalCode);
        Assert.Equal("SIM-CARD-20260707010005-000000003600", approved.TransactionReference);
        Assert.Equal(ApprovedAtUtc, approved.PaymentApprovedAtUtc);
        Assert.Equal(ApprovedAtUtc, approved.LastUpdatedAtUtc);
        Assert.Contains("SIM-CARD-20260707010005-000000003600", approved.PaymentSnapshotJson);
    }

    [Fact]
    public async Task StartAsync_FailedPaymentUpdatesSameRecordWithoutOrder()
    {
        var repository = new RecordingPendingCheckoutRepository();
        var service = Service(repository, new RecordingPaymentSimulator(repository, FailedResult()));

        var result = await service.StartAsync(Cart(), PaymentMethod.Card, PaymentSimulationMode.Fail);
        var failed = repository.Saved[1];

        Assert.False(result.IsApproved);
        Assert.Null(result.OrderId);
        Assert.Equal(PendingCheckoutId, failed.Id);
        Assert.Equal(PendingCheckoutStatus.PaymentFailed, failed.RecoveryStatus);
        Assert.Equal(PaymentStatus.Failed, failed.PaymentStatus);
        Assert.Null(failed.OrderId);
        Assert.Null(failed.ApprovedAmount);
        Assert.Null(failed.ApprovalCode);
        Assert.Null(failed.TransactionReference);
        Assert.Null(failed.PaymentApprovedAtUtc);
        Assert.Contains("Payment was declined by the local simulator.", failed.PaymentSnapshotJson);
    }

    [Fact]
    public async Task StartAsync_RejectsEmptyCart()
    {
        var service = Service(
            new RecordingPendingCheckoutRepository(),
            new RecordingPaymentSimulator(new RecordingPendingCheckoutRepository(), ApprovedResult()));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.StartAsync(new CartSnapshot([], 0m, null, null, 0m, 0m), PaymentMethod.Card));
    }

    private static RecoverablePaymentStartService Service(
        IPendingCheckoutRepository repository,
        IPaymentSimulator simulator) => new(
            repository,
            simulator,
            new StubCheckoutContextProvider(),
            new StubCheckoutClock(CreatedAtUtc),
            new SequenceCheckoutIdGenerator(PendingCheckoutId, OrderId));

    private static CartSnapshot Cart()
    {
        var session = new CheckoutSession();
        session.AddProduct(Product("Cola", 1800m));
        session.AddProduct(Product("Cola", 1800m));
        return session.Snapshot;
    }

    private static Product Product(string name, decimal price) => new(
        Guid.NewGuid(), $"SKU-{name}", Guid.NewGuid().ToString("N"), name, "Beverages", price);

    private static PaymentSimulationResult ApprovedResult() => new(
        PaymentStatus.Approved,
        PaymentMethod.Card,
        3600m,
        3600m,
        "APP-CARD-000000003600",
        "SIM-CARD-20260707010005-000000003600",
        ApprovedAtUtc,
        null);

    private static PaymentSimulationResult FailedResult() => new(
        PaymentStatus.Failed,
        PaymentMethod.Card,
        3600m,
        null,
        null,
        null,
        null,
        "Payment was declined by the local simulator.");

    private sealed class RecordingPaymentSimulator(
        RecordingPendingCheckoutRepository repository,
        PaymentSimulationResult result) : IPaymentSimulator
    {
        public bool WasCalled { get; private set; }

        public Task<PaymentSimulationResult> SimulateAsync(
            PaymentSimulationRequest request,
            CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            Assert.Single(repository.Saved);
            Assert.Equal(PendingCheckoutStatus.AwaitingPayment, repository.Saved[0].RecoveryStatus);
            Assert.Equal(PaymentStatus.Pending, repository.Saved[0].PaymentStatus);
            return Task.FromResult(result);
        }
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
            Task.FromResult<IReadOnlyList<PendingCheckoutRecord>>(Saved);

        public Task MarkCompletedAsync(
            Guid id,
            Guid orderId,
            DateTimeOffset completedAtUtc,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
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
