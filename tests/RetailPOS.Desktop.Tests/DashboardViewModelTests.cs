using RetailPOS.Application.Checkout;
using RetailPOS.Application.Persistence;
using RetailPOS.Application.Sync;
using RetailPOS.Desktop.Sync;
using RetailPOS.Desktop.ViewModels;
using RetailPOS.Domain.Orders;

namespace RetailPOS.Desktop.Tests;

public sealed class DashboardViewModelTests
{
    private static readonly DateTimeOffset NowUtc = new(2026, 7, 9, 3, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task LoadAsync_MapsOperationalSnapshot()
    {
        var today = Today();
        var orders = new RecordingOrderRepository(
            Order("LOCAL-001", today, NowUtc.AddMinutes(-2), 1800m),
            Order("LOCAL-002", today, NowUtc.AddMinutes(-5), 2800m),
            Order("LOCAL-OLD", today.AddDays(-1), NowUtc.AddDays(-1), 900m));
        var queue = new RecordingSyncQueueRepository(
            QueueItem(SyncQueueStatus.Pending, retryCount: 0),
            QueueItem(SyncQueueStatus.Pending, retryCount: 2),
            QueueItem(SyncQueueStatus.Exhausted, retryCount: 5),
            QueueItem(SyncQueueStatus.Resolved, retryCount: 5));
        var recovery = new RecordingCheckoutRecoveryService(recoverableCount: 2);
        var viewModel = ViewModel(orders, queue, recovery, ApiConnectivityStatus.Online);

        await viewModel.LoadAsync();

        Assert.Equal("4,600 KRW", viewModel.NetSalesText);
        Assert.Equal("2", viewModel.OrderCountText);
        Assert.Equal("Average 2,300 KRW", viewModel.AverageOrderText);
        Assert.Equal("1 pending", viewModel.PendingSyncText);
        Assert.Equal("1 retrying", viewModel.RetrySyncText);
        Assert.Equal("1 need review", viewModel.SyncReviewText);
        Assert.Equal("2 checkouts", viewModel.CheckoutRecoveryText);
        Assert.Equal("API online", viewModel.ConnectivityText);
        Assert.Equal("ACTION NEEDED", viewModel.DashboardStatusText);
        Assert.Equal("2 checkouts need recovery", viewModel.AttentionTitle);
        Assert.Equal(3, viewModel.RecentOrders.Count);
        Assert.True(viewModel.HasRecentOrders);
        Assert.False(viewModel.HasNoRecentOrders);
    }

    [Fact]
    public async Task LoadAsync_WithEmptyData_ShowsUsefulZeroState()
    {
        var viewModel = ViewModel(
            new RecordingOrderRepository(),
            new RecordingSyncQueueRepository(),
            new RecordingCheckoutRecoveryService(recoverableCount: 0),
            ApiConnectivityStatus.Online);

        await viewModel.LoadAsync();

        Assert.Equal("0 KRW", viewModel.NetSalesText);
        Assert.Equal("0", viewModel.OrderCountText);
        Assert.Equal("No sales today", viewModel.AverageOrderText);
        Assert.Equal("0 pending", viewModel.PendingSyncText);
        Assert.Equal("0 retrying", viewModel.RetrySyncText);
        Assert.Equal("0 need review", viewModel.SyncReviewText);
        Assert.Equal("0 checkouts", viewModel.CheckoutRecoveryText);
        Assert.Equal("ALL SYSTEMS NORMAL", viewModel.DashboardStatusText);
        Assert.Equal("No operations need attention", viewModel.AttentionTitle);
        Assert.False(viewModel.HasRecentOrders);
        Assert.True(viewModel.HasNoRecentOrders);
    }

    private static DashboardViewModel ViewModel(
        IOrderRepository orderRepository,
        ISyncQueueRepository syncQueueRepository,
        ICheckoutRecoveryService checkoutRecoveryService,
        ApiConnectivityStatus connectivityStatus)
    {
        var connectivity = new ApiConnectivityStateStore();
        connectivity.Update(new ApiConnectivitySnapshot(connectivityStatus, NowUtc, null));
        var clock = new StubOrderSyncClock(NowUtc);
        return new DashboardViewModel(
            orderRepository,
            new SyncStatusService(syncQueueRepository, clock),
            checkoutRecoveryService,
            connectivity,
            clock);
    }

    private static DateOnly Today() => DateOnly.FromDateTime(NowUtc.ToLocalTime().Date);

    private static Order Order(string number, DateOnly businessDate, DateTimeOffset createdAtUtc, decimal total) => new(
        Guid.NewGuid(),
        number,
        Guid.Parse("10000000-0000-0000-0000-000000000001"),
        Guid.Parse("20000000-0000-0000-0000-000000000001"),
        Guid.Parse("30000000-0000-0000-0000-000000000001"),
        businessDate,
        createdAtUtc,
        [new OrderLine(Guid.NewGuid(), "Cola", total, 1)]);

    private static SyncQueueRecord QueueItem(SyncQueueStatus status, int retryCount) => new(
        Guid.NewGuid(),
        "Order",
        Guid.NewGuid(),
        "{}",
        Guid.NewGuid().ToString("N"),
        status,
        retryCount,
        NowUtc,
        null,
        NowUtc.AddMinutes(-5),
        NowUtc.AddMinutes(-1));

    private sealed class RecordingOrderRepository(params Order[] orders) : IOrderRepository
    {
        public Task SaveAsync(Order order, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<Order?> GetByIdAsync(Guid localOrderId, CancellationToken cancellationToken = default) =>
            Task.FromResult(orders.SingleOrDefault(order => order.LocalOrderId == localOrderId));

        public Task<Order?> GetByNumberAsync(string localOrderNumber, CancellationToken cancellationToken = default) =>
            Task.FromResult(orders.SingleOrDefault(order => order.LocalOrderNumber == localOrderNumber));

        public Task<IReadOnlyList<Order>> GetByBusinessDateAsync(
            DateOnly businessDate,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Order>>(orders.Where(order => order.BusinessDate == businessDate).ToArray());

        public Task<IReadOnlyList<Order>> GetRecentAsync(int count, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Order>>(orders
                .OrderByDescending(order => order.CreatedAtUtc)
                .Take(count)
                .ToArray());

        public Task<bool> ExistsAsync(Guid localOrderId, CancellationToken cancellationToken = default) =>
            Task.FromResult(orders.Any(order => order.LocalOrderId == localOrderId));
    }

    private sealed class RecordingSyncQueueRepository(params SyncQueueRecord[] items) : ISyncQueueRepository
    {
        public Task EnqueueAsync(SyncQueueRecord item, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<SyncQueueRecord>> GetDuePendingAsync(
            DateTimeOffset asOfUtc,
            int count,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<SyncQueueRecord>>([]);

        public Task<IReadOnlyList<SyncQueueRecord>> GetRecentAsync(
            int count,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<SyncQueueRecord>>(items.Take(count).ToArray());

        public Task<bool> ExistsByReferenceKeyAsync(string referenceKey, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task UpdateRetryAsync(Guid id, int retryCount, DateTimeOffset nextAttemptAtUtc, string? lastErrorSummary,
            DateTimeOffset updatedAtUtc, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task MarkCompletedAsync(Guid id, DateTimeOffset completedAtUtc, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task MarkResolvedAsync(Guid id, DateTimeOffset resolvedAtUtc, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task MarkExhaustedAsync(Guid id, int retryCount, string? lastErrorSummary, DateTimeOffset exhaustedAtUtc,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class RecordingCheckoutRecoveryService(int recoverableCount) : ICheckoutRecoveryService
    {
        public Task<IReadOnlyList<CheckoutRecoveryRecord>> GetRecoverableAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<CheckoutRecoveryRecord>>(
                Enumerable.Range(0, recoverableCount).Select(_ => Record()).ToArray());

        public Task<CheckoutRecoveryCompletionResult> CompleteAsync(
            Guid pendingCheckoutId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new CheckoutRecoveryCompletionResult(true, Guid.NewGuid(), false, "Recovered."));

        public Task MarkManagerReviewRequiredAsync(
            Guid pendingCheckoutId,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        private static CheckoutRecoveryRecord Record() => new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            NowUtc,
            PendingCheckoutStatus.ApprovedButOrderNotCreated,
            1000m,
            "Card",
            "APP-001",
            "TX-001",
            NowUtc,
            Guid.NewGuid(),
            [new CheckoutRecoveryLine("Cola", 1, 1000m, 1000m)],
            1000m,
            0m,
            1000m,
            true,
            true,
            null);
    }

    private sealed class StubOrderSyncClock(DateTimeOffset utcNow) : IOrderSyncClock
    {
        public DateTimeOffset UtcNow => utcNow;
    }
}
