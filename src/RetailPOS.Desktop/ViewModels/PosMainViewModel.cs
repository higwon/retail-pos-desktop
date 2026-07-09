using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using RetailPOS.Application.Authentication;
using RetailPOS.Application.Checkout;
using RetailPOS.Application.Sync;
using RetailPOS.Desktop.Sync;

namespace RetailPOS.Desktop.ViewModels;

public sealed partial class PosMainViewModel : ObservableObject, IDisposable
{
    private const int SyncSnapshotCount = 50;

    private readonly ICurrentSessionContext _sessionContext;
    private readonly CheckoutSession _checkoutSession;
    private readonly IApiConnectivityStateStore _connectivityStateStore;
    private readonly SyncStatusService _syncStatusService;
    private readonly IMessenger _messenger;
    private int _syncRefreshVersion;
    private bool _disposed;

    public PosMainViewModel(
        ICurrentSessionContext sessionContext,
        CheckoutSession checkoutSession,
        IApiConnectivityStateStore connectivityStateStore,
        SyncStatusService syncStatusService,
        IMessenger messenger)
    {
        _sessionContext = sessionContext;
        _checkoutSession = checkoutSession;
        _connectivityStateStore = connectivityStateStore;
        _syncStatusService = syncStatusService;
        _messenger = messenger;

        _checkoutSession.Changed += OnCheckoutChanged;
        _messenger.Register<ApiConnectivityChangedMessage>(
            this,
            (_, message) => ScheduleOnUi(() => ApplyConnectivity(message.Current)));
        _messenger.Register<SyncStatusChangedMessage>(
            this,
            (_, _) => ScheduleSyncRefresh());
        _messenger.Register<OrderSyncRunCompletedMessage>(
            this,
            (_, _) => ScheduleSyncRefresh());

        RefreshSession();
        RefreshCheckout();
        ApplyConnectivity(_connectivityStateStore.Current);
    }

    [ObservableProperty]
    private string _cashierText = "No cashier signed in";

    [ObservableProperty]
    private string _storeTerminalText = "Store not selected";

    [ObservableProperty]
    private string _connectivityText = "API status unknown";

    [ObservableProperty]
    private string _connectivityBadgeBackground = "#FFE5E7EB";

    [ObservableProperty]
    private string _connectivityBadgeForeground = "#FF374151";

    [ObservableProperty]
    private string _cartSummaryText = "Cart empty";

    [ObservableProperty]
    private string _syncSummaryText = "Sync not checked";

    [ObservableProperty]
    private bool _hasSyncReview;

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        RefreshSession();
        RefreshCheckout();
        ApplyConnectivity(_connectivityStateStore.Current);
        await RefreshSyncAsync(cancellationToken);
    }

    private async Task RefreshSyncAsync(CancellationToken cancellationToken = default)
    {
        var version = Interlocked.Increment(ref _syncRefreshVersion);
        try
        {
            var snapshot = await _syncStatusService.GetSnapshotAsync(SyncSnapshotCount, cancellationToken);
            if (version != Volatile.Read(ref _syncRefreshVersion))
            {
                return;
            }

            HasSyncReview = snapshot.ReviewCount > 0;
            var pendingSyncCount = snapshot.PendingCount + snapshot.RetryCount;
            SyncSummaryText = snapshot.ReviewCount > 0
                ? SyncReviewText(snapshot.ReviewCount)
                : pendingSyncCount > 0
                    ? $"Recent {SyncItemText(pendingSyncCount)} pending"
                    : "Recent sync queue clear";
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch
        {
            if (version != Volatile.Read(ref _syncRefreshVersion))
            {
                return;
            }

            HasSyncReview = false;
            SyncSummaryText = "Recent sync status unavailable";
        }
    }

    private void RefreshSession()
    {
        var session = _sessionContext.Current;
        if (session is null)
        {
            CashierText = "No cashier signed in";
            StoreTerminalText = "Store not selected";
            return;
        }

        CashierText = $"{session.CashierName} ({session.EmployeeCode})";
        StoreTerminalText =
            $"Store {ShortId(session.StoreId)} | Terminal {ShortId(session.TerminalId)}";
    }

    private void RefreshCheckout()
    {
        var snapshot = _checkoutSession.Snapshot;
        CartSummaryText = snapshot.IsEmpty
            ? "Cart empty"
            : $"{snapshot.ItemCount:N0} items | {snapshot.Total:N0} KRW";
    }

    private void ApplyConnectivity(ApiConnectivitySnapshot snapshot)
    {
        (ConnectivityText, ConnectivityBadgeBackground, ConnectivityBadgeForeground) = snapshot.Status switch
        {
            ApiConnectivityStatus.Online => ("API online", "#FFDCFCE7", "#FF166534"),
            ApiConnectivityStatus.Offline => ("API offline", "#FFFEE2E2", "#FFB91C1C"),
            _ => ("API status unknown", "#FFE5E7EB", "#FF374151")
        };
    }

    private void OnCheckoutChanged(object? sender, EventArgs e) => RefreshCheckout();

    private void ScheduleSyncRefresh() =>
        ScheduleOnUi(() => _ = RefreshSyncAsync());

    private void ScheduleOnUi(Action action)
    {
        if (_disposed)
        {
            return;
        }

        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is not null && !dispatcher.CheckAccess())
        {
            _ = dispatcher.BeginInvoke(action);
            return;
        }

        action();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _checkoutSession.Changed -= OnCheckoutChanged;
        _messenger.UnregisterAll(this);
    }

    private static string ShortId(Guid value) => value.ToString("N")[..8].ToUpperInvariant();

    private static string SyncItemText(int count) =>
        count == 1 ? "1 sync item" : $"{count:N0} sync items";

    private static string SyncReviewText(int count) =>
        count == 1 ? "1 sync item needs review" : $"{count:N0} sync items need review";
}
