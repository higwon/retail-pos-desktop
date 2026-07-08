using RetailPOS.Application.Checkout;
using RetailPOS.Application.Persistence;
using RetailPOS.Domain.Payments;

namespace RetailPOS.Application.Tests;

public sealed class CheckoutRecoveryServiceTests
{
    private static readonly Guid PendingCheckoutId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid OrderId = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001");
    private static readonly DateTimeOffset Now = new(2026, 7, 8, 1, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task GetRecoverableAsync_ReturnsOnlyApprovedUnresolvedRecords()
    {
        var repository = new RecordingPendingCheckoutRepository(
            ApprovedCheckout(PendingCheckoutId),
            ApprovedCheckout(Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002")) with
            {
                RecoveryStatus = PendingCheckoutStatus.PaymentFailed,
                PaymentStatus = PaymentStatus.Failed
            },
            ApprovedCheckout(Guid.Parse("aaaaaaaa-0000-0000-0000-000000000003")) with
            {
                RecoveryStatus = PendingCheckoutStatus.ManagerReviewRequired
            });
        var service = new CheckoutRecoveryService(
            repository,
            new RecordingOrderCompletionService(),
            new StubCheckoutClock(Now));

        var records = await service.GetRecoverableAsync();

        var record = Assert.Single(records);
        Assert.Equal(PendingCheckoutId, record.PendingCheckoutId);
        Assert.True(record.IsSnapshotReadable);
        Assert.Equal(3600m, record.CartTotal);
        Assert.Equal("Cola", Assert.Single(record.Lines).ProductName);
    }

    [Fact]
    public async Task CompleteAsync_UsesOrderCompletionBoundary()
    {
        var completion = new RecordingOrderCompletionService();
        var service = new CheckoutRecoveryService(
            new RecordingPendingCheckoutRepository(ApprovedCheckout(PendingCheckoutId)),
            completion,
            new StubCheckoutClock(Now));

        var result = await service.CompleteAsync(PendingCheckoutId);

        Assert.True(result.Succeeded);
        Assert.Equal(PendingCheckoutId, completion.CompletedPendingCheckoutId);
        Assert.Equal(OrderId, result.LocalOrderId);
    }

    [Fact]
    public async Task CompleteAsync_ReturnsUserSafeMessageWhenCompletionFails()
    {
        var completion = new RecordingOrderCompletionService(throwOnComplete: true);
        var service = new CheckoutRecoveryService(
            new RecordingPendingCheckoutRepository(ApprovedCheckout(PendingCheckoutId) with
            {
                CartSnapshotJson = "{invalid"
            }),
            completion,
            new StubCheckoutClock(Now));

        var result = await service.CompleteAsync(PendingCheckoutId);

        Assert.False(result.Succeeded);
        Assert.Contains("manager review", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MarkManagerReviewRequiredAsync_UpdatesPendingCheckoutStatus()
    {
        var repository = new RecordingPendingCheckoutRepository(ApprovedCheckout(PendingCheckoutId));
        var service = new CheckoutRecoveryService(
            repository,
            new RecordingOrderCompletionService(),
            new StubCheckoutClock(Now));

        await service.MarkManagerReviewRequiredAsync(PendingCheckoutId);

        Assert.Equal(PendingCheckoutStatus.ManagerReviewRequired, repository.Records.Single().RecoveryStatus);
        Assert.Equal(Now, repository.Records.Single().LastUpdatedAtUtc);
    }

    private static PendingCheckoutRecord ApprovedCheckout(Guid id) => new(
        id,
        Guid.Parse("10000000-0000-0000-0000-000000000001"),
        Guid.Parse("20000000-0000-0000-0000-000000000001"),
        Guid.Parse("30000000-0000-0000-0000-000000000001"),
        Now.AddMinutes(-5),
        PendingCheckoutStatus.ApprovedButOrderNotCreated,
        """
        {"lines":[{"productId":"11111111-0000-0000-0000-000000000001","productName":"Cola","unitPrice":1800,"quantity":2,"lineTotal":3600}],"subtotal":3600,"discountType":null,"discountValue":null,"discountAmount":0,"total":3600}
        """,
        """
        {"method":"Card","requestedAmount":3600,"status":"Approved","approvedAmount":3600,"approvalCode":"APP-001","transactionReference":"TX-001","approvedAtUtc":"2026-07-08T00:56:00+00:00","failureMessage":null}
        """,
        PaymentStatus.Approved,
        "APP-001",
        3600m,
        "TX-001",
        Now.AddMinutes(-4),
        OrderId,
        null,
        Now.AddMinutes(-4));

    private sealed class RecordingPendingCheckoutRepository(params PendingCheckoutRecord[] records)
        : IPendingCheckoutRepository
    {
        public List<PendingCheckoutRecord> Records { get; } = records.ToList();

        public Task SaveAsync(PendingCheckoutRecord checkout, CancellationToken cancellationToken = default)
        {
            Records.RemoveAll(record => record.Id == checkout.Id);
            Records.Add(checkout);
            return Task.CompletedTask;
        }

        public Task<PendingCheckoutRecord?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Records.SingleOrDefault(record => record.Id == id));

        public Task<IReadOnlyList<PendingCheckoutRecord>> GetUnresolvedAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PendingCheckoutRecord>>(
                Records.Where(record => record.RecoveryStatus != PendingCheckoutStatus.Completed).ToList());

        public Task MarkCompletedAsync(
            Guid id,
            Guid orderId,
            DateTimeOffset completedAtUtc,
            CancellationToken cancellationToken = default)
        {
            var record = Records.Single(item => item.Id == id);
            Records.Remove(record);
            Records.Add(record with
            {
                RecoveryStatus = PendingCheckoutStatus.Completed,
                OrderId = orderId,
                CompletedAtUtc = completedAtUtc,
                LastUpdatedAtUtc = completedAtUtc
            });
            return Task.CompletedTask;
        }

        public Task MarkManagerReviewRequiredAsync(
            Guid id,
            DateTimeOffset updatedAtUtc,
            CancellationToken cancellationToken = default)
        {
            var record = Records.Single(item => item.Id == id);
            Records.Remove(record);
            Records.Add(record with
            {
                RecoveryStatus = PendingCheckoutStatus.ManagerReviewRequired,
                LastUpdatedAtUtc = updatedAtUtc
            });
            return Task.CompletedTask;
        }

        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            Records.RemoveAll(record => record.Id == id);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingOrderCompletionService(bool throwOnComplete = false) : IOrderCompletionService
    {
        public Guid? CompletedPendingCheckoutId { get; private set; }

        public Task<OrderCompletionResult> CompleteAsync(
            Guid pendingCheckoutId,
            CancellationToken cancellationToken = default)
        {
            if (throwOnComplete)
            {
                throw new InvalidOperationException("Simulated recovery failure with technical details.");
            }

            CompletedPendingCheckoutId = pendingCheckoutId;
            return Task.FromResult(new OrderCompletionResult(OrderId, AlreadyCompleted: false));
        }
    }

    private sealed class StubCheckoutClock(DateTimeOffset utcNow) : ICheckoutClock
    {
        public DateTimeOffset UtcNow => utcNow;
    }
}
