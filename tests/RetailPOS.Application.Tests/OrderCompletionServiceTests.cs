using RetailPOS.Application.Checkout;
using RetailPOS.Application.Persistence;
using RetailPOS.Domain.Orders;
using RetailPOS.Domain.Payments;
using System.Text.Json;

namespace RetailPOS.Application.Tests;

public sealed class OrderCompletionServiceTests
{
    private static readonly Guid PendingCheckoutId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid OrderId = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001");
    private static readonly Guid StoreId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid TerminalId = Guid.Parse("20000000-0000-0000-0000-000000000001");
    private static readonly Guid CashierId = Guid.Parse("30000000-0000-0000-0000-000000000001");
    private static readonly DateTimeOffset CreatedAtUtc = new(2026, 7, 7, 1, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset ApprovedAtUtc = new(2026, 7, 7, 1, 0, 5, TimeSpan.Zero);
    private static readonly DateTimeOffset CompletedAtUtc = new(2026, 7, 7, 1, 0, 8, TimeSpan.Zero);

    [Fact]
    public async Task CompleteAsync_SavesOrderMarksPendingAndEnqueuesUpload()
    {
        var pending = new RecordingPendingCheckoutRepository(ApprovedCheckout());
        var orders = new RecordingOrderRepository();
        var queue = new RecordingSyncQueueRepository();
        var transaction = new RecordingLocalTransaction();
        var service = Service(pending, orders, queue, transaction);

        var result = await service.CompleteAsync(PendingCheckoutId);

        Assert.False(result.AlreadyCompleted);
        Assert.Equal(OrderId, result.LocalOrderId);
        Assert.True(transaction.WasUsed);
        var order = Assert.Single(orders.Saved);
        Assert.Equal(OrderId, order.LocalOrderId);
        Assert.Equal(3400m, order.TotalAmount);
        Assert.Equal(200m, order.DiscountAmount);
        Assert.Equal(PaymentStatus.Approved, Assert.Single(order.Payments).Status);
        Assert.Equal(PendingCheckoutStatus.Completed, pending.Record.RecoveryStatus);
        Assert.Equal(OrderId, pending.Record.OrderId);
        var item = Assert.Single(queue.Items);
        Assert.Equal("Order", item.ItemType);
        Assert.Equal(OrderId, item.AggregateId);
        Assert.Contains(OrderId.ToString("N"), item.ReferenceKey);
        using var payload = Payload(item);
        var root = payload.RootElement;
        Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());
        Assert.Equal(StoreId, root.GetProperty("storeId").GetGuid());
        Assert.Equal(TerminalId, root.GetProperty("terminalId").GetGuid());
        Assert.Equal(OrderId, root.GetProperty("localOrderId").GetGuid());
        Assert.Equal($"{StoreId:N}:{TerminalId:N}:{OrderId:N}", root.GetProperty("idempotencyKey").GetString());
        Assert.Equal(order.LocalOrderNumber, root.GetProperty("localOrderNumber").GetString());
        Assert.Equal("2026-07-07", root.GetProperty("businessDate").GetString());
        Assert.Equal(CashierId, root.GetProperty("cashierId").GetGuid());
        Assert.Equal(3600m, root.GetProperty("subtotalAmount").GetDecimal());
        Assert.Equal(200m, root.GetProperty("discountAmount").GetDecimal());
        Assert.Equal(3400m, root.GetProperty("totalAmount").GetDecimal());
        Assert.Equal(ApprovedAtUtc, root.GetProperty("createdAt").GetDateTimeOffset());

        var line = Assert.Single(root.GetProperty("lines").EnumerateArray());
        Assert.Equal(Guid.Parse("11111111-0000-0000-0000-000000000001"), line.GetProperty("productId").GetGuid());
        Assert.Equal("Cola", line.GetProperty("productNameSnapshot").GetString());
        Assert.Equal(1800m, line.GetProperty("unitPrice").GetDecimal());
        Assert.Equal(2, line.GetProperty("quantity").GetInt32());
        Assert.Equal(200m, line.GetProperty("lineDiscountAmount").GetDecimal());
        Assert.Equal(3400m, line.GetProperty("lineTotalAmount").GetDecimal());

        var payment = Assert.Single(root.GetProperty("payments").EnumerateArray());
        Assert.Equal("Card", payment.GetProperty("paymentMethod").GetString());
        Assert.Equal(3400m, payment.GetProperty("approvedAmount").GetDecimal());
        Assert.Equal("APP-001", payment.GetProperty("approvalCode").GetString());
        Assert.Equal("TX-001", payment.GetProperty("transactionReference").GetString());
        Assert.Equal(ApprovedAtUtc, payment.GetProperty("approvedAtUtc").GetDateTimeOffset());
    }

    [Fact]
    public async Task CompleteAsync_IsIdempotentWhenOrderAlreadyExists()
    {
        var pending = new RecordingPendingCheckoutRepository(ApprovedCheckout());
        var orders = new RecordingOrderRepository();
        orders.Saved.Add(CompletedOrder());
        orders.ExistingIds.Add(OrderId);
        var queue = new RecordingSyncQueueRepository();
        var transaction = new RecordingLocalTransaction();
        var service = Service(pending, orders, queue, transaction);

        var result = await service.CompleteAsync(PendingCheckoutId);

        Assert.True(result.AlreadyCompleted);
        Assert.Equal(OrderId, result.LocalOrderId);
        Assert.True(transaction.WasUsed);
        Assert.Single(orders.Saved);
        Assert.Equal(PendingCheckoutStatus.Completed, pending.Record.RecoveryStatus);
        Assert.Equal(OrderId, pending.Record.OrderId);
        var item = Assert.Single(queue.Items);
        using var payload = Payload(item);
        Assert.Equal(1, payload.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.Equal(3400m, payload.RootElement.GetProperty("totalAmount").GetDecimal());
    }

    [Fact]
    public async Task CompleteAsync_DoesNotDuplicateSyncQueueWhenOrderAlreadyExists()
    {
        var pending = new RecordingPendingCheckoutRepository(ApprovedCheckout());
        var orders = new RecordingOrderRepository();
        orders.Saved.Add(CompletedOrder());
        orders.ExistingIds.Add(OrderId);
        var queue = new RecordingSyncQueueRepository();
        queue.Items.Add(new SyncQueueRecord(
            Guid.Parse("dddddddd-0000-0000-0000-000000000001"),
            "Order",
            OrderId,
            null,
            $"{StoreId:N}:{TerminalId:N}:{OrderId:N}",
            SyncQueueStatus.Pending,
            0,
            CompletedAtUtc,
            null,
            CompletedAtUtc,
            CompletedAtUtc));
        var service = Service(pending, orders, queue, new RecordingLocalTransaction());

        var result = await service.CompleteAsync(PendingCheckoutId);

        Assert.True(result.AlreadyCompleted);
        Assert.Single(queue.Items);
        Assert.Equal(PendingCheckoutStatus.Completed, pending.Record.RecoveryStatus);
    }

    [Fact]
    public async Task CompleteAsync_RejectsNonApprovedCheckout()
    {
        var pending = ApprovedCheckout() with
        {
            RecoveryStatus = PendingCheckoutStatus.PaymentFailed,
            PaymentStatus = PaymentStatus.Failed,
            OrderId = null
        };
        var service = Service(
            new RecordingPendingCheckoutRepository(pending),
            new RecordingOrderRepository(),
            new RecordingSyncQueueRepository(),
            new RecordingLocalTransaction());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CompleteAsync(PendingCheckoutId));
    }

    private static OrderCompletionService Service(
        IPendingCheckoutRepository pending,
        IOrderRepository orders,
        ISyncQueueRepository queue,
        ILocalTransaction transaction) => new(
            pending,
            orders,
            queue,
            transaction,
            new StubCheckoutClock(CompletedAtUtc),
            new SequenceCheckoutIdGenerator(Guid.Parse("cccccccc-0000-0000-0000-000000000001")));

    private static JsonDocument Payload(SyncQueueRecord item) =>
        JsonDocument.Parse(item.PayloadJson ?? throw new InvalidOperationException("Sync queue payload is required."));

    private static PendingCheckoutRecord ApprovedCheckout() => new(
        PendingCheckoutId,
        StoreId,
        TerminalId,
        CashierId,
        CreatedAtUtc,
        PendingCheckoutStatus.ApprovedButOrderNotCreated,
        """
        {"lines":[{"productId":"11111111-0000-0000-0000-000000000001","productName":"Cola","unitPrice":1800,"quantity":2,"lineTotal":3600}],"subtotal":3600,"discountType":"FixedAmount","discountValue":200,"discountAmount":200,"total":3400}
        """,
        """
        {"method":"Card","requestedAmount":3400,"status":"Approved","approvedAmount":3400,"approvalCode":"APP-001","transactionReference":"TX-001","approvedAtUtc":"2026-07-07T01:00:05+00:00","failureMessage":null}
        """,
        PaymentStatus.Approved,
        "APP-001",
        3400m,
        "TX-001",
        ApprovedAtUtc,
        OrderId,
        null,
        ApprovedAtUtc);

    private static Order CompletedOrder()
    {
        var payment = new Payment(Guid.NewGuid(), PaymentMethod.Card, 3400m, ApprovedAtUtc);
        payment.Approve(3400m, ApprovedAtUtc, "APP-001", "TX-001");
        return new Order(
            OrderId,
            "LOCAL-20260707-BBBBBBBBBBBB",
            StoreId,
            TerminalId,
            CashierId,
            new DateOnly(2026, 7, 7),
            ApprovedAtUtc,
            [new OrderLine(Guid.Parse("11111111-0000-0000-0000-000000000001"), "Cola", 1800m, 2, 200m)],
            [payment]);
    }

    private sealed class RecordingPendingCheckoutRepository(PendingCheckoutRecord record) : IPendingCheckoutRepository
    {
        public PendingCheckoutRecord Record { get; private set; } = record;

        public Task SaveAsync(PendingCheckoutRecord checkout, CancellationToken cancellationToken = default)
        {
            Record = checkout;
            return Task.CompletedTask;
        }

        public Task<PendingCheckoutRecord?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(id == Record.Id ? Record : null);

        public Task<IReadOnlyList<PendingCheckoutRecord>> GetUnresolvedAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PendingCheckoutRecord>>([Record]);

        public Task MarkCompletedAsync(Guid id, Guid orderId, DateTimeOffset completedAtUtc,
            CancellationToken cancellationToken = default)
        {
            Record = Record with
            {
                RecoveryStatus = PendingCheckoutStatus.Completed,
                OrderId = orderId,
                CompletedAtUtc = completedAtUtc,
                LastUpdatedAtUtc = completedAtUtc
            };
            return Task.CompletedTask;
        }

        public Task MarkManagerReviewRequiredAsync(
            Guid id,
            DateTimeOffset updatedAtUtc,
            CancellationToken cancellationToken = default)
        {
            Record = Record with
            {
                RecoveryStatus = PendingCheckoutStatus.ManagerReviewRequired,
                LastUpdatedAtUtc = updatedAtUtc
            };
            return Task.CompletedTask;
        }

        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class RecordingOrderRepository : IOrderRepository
    {
        public List<Order> Saved { get; } = [];
        public HashSet<Guid> ExistingIds { get; } = [];

        public Task SaveAsync(Order order, CancellationToken cancellationToken = default)
        {
            Saved.Add(order);
            ExistingIds.Add(order.LocalOrderId);
            return Task.CompletedTask;
        }

        public Task<Order?> GetByIdAsync(Guid localOrderId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Saved.SingleOrDefault(order => order.LocalOrderId == localOrderId));

        public Task<Order?> GetByNumberAsync(string localOrderNumber, CancellationToken cancellationToken = default) =>
            Task.FromResult(Saved.SingleOrDefault(order => order.LocalOrderNumber == localOrderNumber));

        public Task<IReadOnlyList<Order>> GetByBusinessDateAsync(
            DateOnly businessDate,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Order>>(Saved.Where(order => order.BusinessDate == businessDate).ToArray());

        public Task<IReadOnlyList<Order>> GetRecentAsync(int count, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Order>>(Saved.Take(count).ToArray());

        public Task<bool> ExistsAsync(Guid localOrderId, CancellationToken cancellationToken = default) =>
            Task.FromResult(ExistingIds.Contains(localOrderId));
    }

    private sealed class RecordingSyncQueueRepository : ISyncQueueRepository
    {
        public List<SyncQueueRecord> Items { get; } = [];

        public Task EnqueueAsync(SyncQueueRecord item, CancellationToken cancellationToken = default)
        {
            Items.Add(item);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<SyncQueueRecord>> GetDuePendingAsync(DateTimeOffset asOfUtc, int count,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<SyncQueueRecord>>(Items.Take(count).ToArray());

        public Task<IReadOnlyList<SyncQueueRecord>> GetRecentAsync(int count,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<SyncQueueRecord>>(Items.Take(count).ToArray());

        public Task<bool> ExistsByReferenceKeyAsync(string referenceKey, CancellationToken cancellationToken = default) =>
            Task.FromResult(Items.Any(item => item.ReferenceKey == referenceKey));

        public Task UpdateRetryAsync(Guid id, int retryCount, DateTimeOffset nextAttemptAtUtc,
            string? lastErrorSummary, DateTimeOffset updatedAtUtc, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task MarkCompletedAsync(Guid id, DateTimeOffset completedAtUtc,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task MarkResolvedAsync(Guid id, DateTimeOffset resolvedAtUtc,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task MarkExhaustedAsync(Guid id, int retryCount, string? lastErrorSummary,
            DateTimeOffset exhaustedAtUtc, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class RecordingLocalTransaction : ILocalTransaction
    {
        public bool WasUsed { get; private set; }

        public async Task ExecuteAsync(Func<CancellationToken, Task> operation,
            CancellationToken cancellationToken = default)
        {
            WasUsed = true;
            await operation(cancellationToken);
        }
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
