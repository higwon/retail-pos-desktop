using CommunityToolkit.Mvvm.Messaging;
using RetailPOS.Application.Authentication;
using RetailPOS.Application.Checkout;
using RetailPOS.Application.Persistence;
using RetailPOS.Application.Sync;
using RetailPOS.Desktop.Sync;
using RetailPOS.Desktop.ViewModels;
using RetailPOS.Domain.Products;

namespace RetailPOS.Desktop.Tests;

public sealed class PosMainViewModelTests
{
    private static readonly DateTimeOffset NowUtc = new(2026, 7, 9, 3, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task LoadAsync_MapsCurrentSessionConnectivityCartAndSyncState()
    {
        var sessionContext = SignedInSession();
        var checkoutSession = new CheckoutSession();
        checkoutSession.AddProduct(Product("Cola", 1800m));
        checkoutSession.AddProduct(Product("Cola", 1800m));
        var connectivity = Connectivity(ApiConnectivityStatus.Online);
        var syncQueue = new RecordingSyncQueueRepository(
            QueueItem(SyncQueueStatus.Pending, retryCount: 0),
            QueueItem(SyncQueueStatus.Pending, retryCount: 2));
        var viewModel = ViewModel(sessionContext, checkoutSession, connectivity, syncQueue);

        await viewModel.LoadAsync();

        Assert.Equal("Cashier A (E0001)", viewModel.CashierText);
        Assert.Equal("Store 10000000 | Terminal 20000000", viewModel.StoreTerminalText);
        Assert.Equal("API online", viewModel.ConnectivityText);
        Assert.Equal("API ONLINE", viewModel.ConnectivityBadgeText);
        Assert.Equal("#FFDCFCE7", viewModel.ConnectivityBadgeBackground);
        Assert.Equal("#FF166534", viewModel.ConnectivityBadgeForeground);
        Assert.Equal("2 items | 3,600 KRW", viewModel.CartSummaryText);
        Assert.Equal("Recent 2 sync items pending", viewModel.SyncSummaryText);
        Assert.False(viewModel.HasSyncReview);
    }

    [Fact]
    public void CheckoutChanges_UpdateCartSummary()
    {
        var checkoutSession = new CheckoutSession();
        var viewModel = ViewModel(SignedInSession(), checkoutSession);

        checkoutSession.AddProduct(Product("Water", 1000m));

        Assert.Equal("1 items | 1,000 KRW", viewModel.CartSummaryText);
    }

    [Fact]
    public void Activate_RefreshesCashierAfterSignOutAndRelogin()
    {
        var session = SignedInSession();
        var viewModel = ViewModel(session);
        session.Clear();
        session.SignIn(new CashierSession(
            Guid.Parse("40000000-0000-0000-0000-000000000001"),
            Guid.Parse("50000000-0000-0000-0000-000000000001"),
            Guid.Parse("60000000-0000-0000-0000-000000000001"),
            "E0002",
            "Cashier B",
            NowUtc));

        viewModel.Activate();

        Assert.Equal("Cashier B (E0002)", viewModel.CashierText);
        Assert.Equal("Store 40000000 | Terminal 50000000", viewModel.StoreTerminalText);
    }

    [Fact]
    public async Task SyncReviewCount_IsExposedForHeader()
    {
        var syncQueue = new RecordingSyncQueueRepository(
            QueueItem(SyncQueueStatus.Exhausted, retryCount: 5));
        var viewModel = ViewModel(SignedInSession(), syncQueue: syncQueue);

        await viewModel.LoadAsync();

        Assert.True(viewModel.HasSyncReview);
        Assert.Equal("1 sync item needs review", viewModel.SyncSummaryText);
    }

    [Fact]
    public void ApiConnectivityMessage_UpdatesConnectivityText()
    {
        var messenger = new WeakReferenceMessenger();
        var connectivity = Connectivity(ApiConnectivityStatus.Unknown);
        var viewModel = ViewModel(
            SignedInSession(),
            connectivityStateStore: connectivity,
            messenger: messenger);

        messenger.Send(new ApiConnectivityChangedMessage(
            connectivity.Current,
            new ApiConnectivitySnapshot(ApiConnectivityStatus.Offline, NowUtc, "HttpRequestException")));

        Assert.Equal("API offline", viewModel.ConnectivityText);
        Assert.Equal("API OFFLINE", viewModel.ConnectivityBadgeText);
        Assert.Equal("#FFFEE2E2", viewModel.ConnectivityBadgeBackground);
        Assert.Equal("#FFB91C1C", viewModel.ConnectivityBadgeForeground);
    }

    [Fact]
    public async Task ConcurrentSyncRefresh_DoesNotLetOlderSnapshotOverwriteNewerState()
    {
        var syncQueue = new GatedSyncQueueRepository();
        var viewModel = ViewModel(SignedInSession(), syncQueue: syncQueue);

        var olderLoad = viewModel.LoadAsync();
        await syncQueue.WaitForCallCountAsync(1);
        var newerLoad = viewModel.LoadAsync();
        await syncQueue.WaitForCallCountAsync(2);

        syncQueue.CompleteCall(1, [QueueItem(SyncQueueStatus.Pending, retryCount: 0)]);
        await newerLoad;
        Assert.Equal("Recent 1 sync item pending", viewModel.SyncSummaryText);

        syncQueue.CompleteCall(0, [QueueItem(SyncQueueStatus.Exhausted, retryCount: 5)]);
        await olderLoad;

        Assert.Equal("Recent 1 sync item pending", viewModel.SyncSummaryText);
        Assert.False(viewModel.HasSyncReview);
    }

    [Fact]
    public void Dispose_UnsubscribesFromCheckoutAndMessenger()
    {
        var messenger = new WeakReferenceMessenger();
        var checkoutSession = new CheckoutSession();
        var viewModel = ViewModel(
            SignedInSession(),
            checkoutSession,
            messenger: messenger);

        viewModel.Dispose();
        checkoutSession.AddProduct(Product("Water", 1000m));
        messenger.Send(new ApiConnectivityChangedMessage(
            new ApiConnectivitySnapshot(ApiConnectivityStatus.Unknown, NowUtc, null),
            new ApiConnectivitySnapshot(ApiConnectivityStatus.Offline, NowUtc, "timeout")));

        Assert.Equal("Cart empty", viewModel.CartSummaryText);
        Assert.Equal("API status unknown", viewModel.ConnectivityText);
    }

    private static PosMainViewModel ViewModel(
        ICurrentSessionContext sessionContext,
        CheckoutSession? checkoutSession = null,
        IApiConnectivityStateStore? connectivityStateStore = null,
        ISyncQueueRepository? syncQueue = null,
        IMessenger? messenger = null)
    {
        var queue = syncQueue ?? new RecordingSyncQueueRepository();
        return new PosMainViewModel(
            sessionContext,
            checkoutSession ?? new CheckoutSession(),
            connectivityStateStore ?? Connectivity(ApiConnectivityStatus.Unknown),
            new SyncStatusService(queue, new StubOrderSyncClock(NowUtc)),
            messenger ?? new WeakReferenceMessenger());
    }

    private static CurrentSessionContext SignedInSession()
    {
        var context = new CurrentSessionContext();
        context.SignIn(new CashierSession(
            Guid.Parse("10000000-0000-0000-0000-000000000001"),
            Guid.Parse("20000000-0000-0000-0000-000000000001"),
            Guid.Parse("30000000-0000-0000-0000-000000000001"),
            "E0001",
            "Cashier A",
            NowUtc));
        return context;
    }

    private static ApiConnectivityStateStore Connectivity(ApiConnectivityStatus status)
    {
        var store = new ApiConnectivityStateStore();
        store.Update(new ApiConnectivitySnapshot(status, NowUtc, null));
        return store;
    }

    private static Product Product(string name, decimal price) => new(
        Guid.NewGuid(),
        $"SKU-{name}",
        Guid.NewGuid().ToString("N"),
        name,
        "Beverages",
        price);

    private static SyncQueueRecord QueueItem(
        SyncQueueStatus status,
        int retryCount) => new(
        Guid.NewGuid(),
        "Order",
        Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001"),
        "{}",
        Guid.NewGuid().ToString("N"),
        status,
        retryCount,
        NowUtc,
        null,
        NowUtc.AddMinutes(-5),
        NowUtc.AddMinutes(-1));

    private sealed class RecordingSyncQueueRepository(params SyncQueueRecord[] items) : ISyncQueueRepository
    {
        private readonly IReadOnlyList<SyncQueueRecord> _items = items;

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
            Task.FromResult(_items);

        public Task<bool> ExistsByReferenceKeyAsync(
            string referenceKey,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task UpdateRetryAsync(
            Guid id,
            int retryCount,
            DateTimeOffset nextAttemptAtUtc,
            string? lastErrorSummary,
            DateTimeOffset updatedAtUtc,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task MarkCompletedAsync(
            Guid id,
            DateTimeOffset completedAtUtc,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task MarkResolvedAsync(
            Guid id,
            DateTimeOffset resolvedAtUtc,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task MarkExhaustedAsync(
            Guid id,
            int retryCount,
            string? lastErrorSummary,
            DateTimeOffset exhaustedAtUtc,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class GatedSyncQueueRepository : ISyncQueueRepository
    {
        private readonly List<TaskCompletionSource<IReadOnlyList<SyncQueueRecord>>> _calls = [];

        public int CallCount => _calls.Count;

        public Task EnqueueAsync(SyncQueueRecord item, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<SyncQueueRecord>> GetDuePendingAsync(
            DateTimeOffset asOfUtc,
            int count,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<SyncQueueRecord>>([]);

        public Task<IReadOnlyList<SyncQueueRecord>> GetRecentAsync(
            int count,
            CancellationToken cancellationToken = default)
        {
            var completion = new TaskCompletionSource<IReadOnlyList<SyncQueueRecord>>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            _calls.Add(completion);
            return completion.Task;
        }

        public async Task WaitForCallCountAsync(int count)
        {
            for (var attempt = 0; attempt < 20; attempt++)
            {
                if (CallCount >= count)
                {
                    return;
                }

                await Task.Delay(25);
            }

            Assert.True(CallCount >= count);
        }

        public void CompleteCall(int index, IReadOnlyList<SyncQueueRecord> items) =>
            _calls[index].SetResult(items);

        public Task<bool> ExistsByReferenceKeyAsync(
            string referenceKey,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task UpdateRetryAsync(
            Guid id,
            int retryCount,
            DateTimeOffset nextAttemptAtUtc,
            string? lastErrorSummary,
            DateTimeOffset updatedAtUtc,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task MarkCompletedAsync(
            Guid id,
            DateTimeOffset completedAtUtc,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task MarkResolvedAsync(
            Guid id,
            DateTimeOffset resolvedAtUtc,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task MarkExhaustedAsync(
            Guid id,
            int retryCount,
            string? lastErrorSummary,
            DateTimeOffset exhaustedAtUtc,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class StubOrderSyncClock(DateTimeOffset utcNow) : IOrderSyncClock
    {
        public DateTimeOffset UtcNow => utcNow;
    }
}
