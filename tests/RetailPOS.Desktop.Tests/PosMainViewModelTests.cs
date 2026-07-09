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
        Assert.Equal("2 items | 3,600 KRW", viewModel.CartSummaryText);
        Assert.Equal("2 sync items pending", viewModel.SyncSummaryText);
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
        RecordingSyncQueueRepository? syncQueue = null,
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

    private sealed class StubOrderSyncClock(DateTimeOffset utcNow) : IOrderSyncClock
    {
        public DateTimeOffset UtcNow => utcNow;
    }
}
