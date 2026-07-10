using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RetailPOS.Application.Checkout;
using RetailPOS.Application.Persistence;
using RetailPOS.Application.Sync;
using RetailPOS.Desktop.Sync;
using RetailPOS.Domain.Orders;

namespace RetailPOS.Desktop.ViewModels;

public sealed partial class DashboardViewModel : ObservableObject
{
    private const int RecentOrderCount = 5;
    private const int SyncSnapshotCount = 50;

    private readonly IOrderRepository _orderRepository;
    private readonly SyncStatusService _syncStatusService;
    private readonly ICheckoutRecoveryService _checkoutRecoveryService;
    private readonly IApiConnectivityStateStore _connectivityStateStore;
    private readonly IOrderSyncClock _clock;

    public DashboardViewModel(
        IOrderRepository orderRepository,
        SyncStatusService syncStatusService,
        ICheckoutRecoveryService checkoutRecoveryService,
        IApiConnectivityStateStore connectivityStateStore,
        IOrderSyncClock clock)
    {
        _orderRepository = orderRepository;
        _syncStatusService = syncStatusService;
        _checkoutRecoveryService = checkoutRecoveryService;
        _connectivityStateStore = connectivityStateStore;
        _clock = clock;
        RefreshCommand = new AsyncRelayCommand(LoadAsync, () => !IsBusy);
        RecentOrders.CollectionChanged += OnRecentOrdersChanged;
    }

    public ObservableCollection<DashboardRecentOrderViewModel> RecentOrders { get; } = [];
    public IAsyncRelayCommand RefreshCommand { get; }

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string _lastUpdatedText = "Not checked";

    [ObservableProperty]
    private string _netSalesText = "0 KRW";

    [ObservableProperty]
    private string _orderCountText = "0";

    [ObservableProperty]
    private string _averageOrderText = "No sales today";

    [ObservableProperty]
    private string _pendingSyncText = "0 pending";

    [ObservableProperty]
    private string _retrySyncText = "0 retrying";

    [ObservableProperty]
    private string _syncReviewText = "0 need review";

    [ObservableProperty]
    private string _checkoutRecoveryText = "0 checkouts";

    [ObservableProperty]
    private string _connectivityText = "API status unknown";

    [ObservableProperty]
    private string _dashboardStatusText = "STATUS UNKNOWN";

    [ObservableProperty]
    private string _dashboardStatusForeground = "#FF374151";

    [ObservableProperty]
    private string _dashboardStatusBackground = "#FFE5E7EB";

    [ObservableProperty]
    private string _attentionTitle = "No operations need attention";

    [ObservableProperty]
    private string _attentionDetail = "Orders, sync queue, and recovery records are clear.";

    [ObservableProperty]
    private string _attentionBackground = "#FFECFDF3";

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public bool HasRecentOrders => RecentOrders.Count > 0;
    public bool HasNoRecentOrders => RecentOrders.Count == 0;
    public string EmptyRecentOrdersText => "No local orders found yet.";

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        ErrorMessage = null;

        try
        {
            var nowUtc = UtcNow();
            var businessDate = DateOnly.FromDateTime(nowUtc.ToLocalTime().Date);
            var todaysOrders = await _orderRepository.GetByBusinessDateAsync(businessDate, cancellationToken);
            var recentOrders = await _orderRepository.GetRecentAsync(RecentOrderCount, cancellationToken);
            var sync = await _syncStatusService.GetSnapshotAsync(SyncSnapshotCount, cancellationToken);
            var recoverable = await _checkoutRecoveryService.GetRecoverableAsync(cancellationToken);

            ApplyOrders(todaysOrders, recentOrders);
            ApplySync(sync);
            ApplyRecovery(recoverable.Count);
            ApplyConnectivity(_connectivityStateStore.Current);
            ApplyAttention(sync, recoverable.Count);
            LastUpdatedText = nowUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch
        {
            ErrorMessage = "Dashboard data could not be loaded. Try again.";
        }
        finally
        {
            IsBusy = false;
            RefreshCommand.NotifyCanExecuteChanged();
        }
    }

    private void ApplyOrders(IReadOnlyList<Order> todaysOrders, IReadOnlyList<Order> recentOrders)
    {
        var netSales = todaysOrders.Sum(order => order.TotalAmount);
        NetSalesText = $"{netSales:N0} KRW";
        OrderCountText = todaysOrders.Count.ToString("N0");
        AverageOrderText = todaysOrders.Count == 0
            ? "No sales today"
            : $"Average {netSales / todaysOrders.Count:N0} KRW";

        RecentOrders.Clear();
        foreach (var order in recentOrders.Select(order => new DashboardRecentOrderViewModel(order)))
        {
            RecentOrders.Add(order);
        }

        NotifyRecentOrderProperties();
    }

    private void ApplySync(SyncStatusSnapshot sync)
    {
        var exhaustedCount = SyncReviewRequiredCount(sync);
        PendingSyncText = $"{sync.PendingCount:N0} pending";
        RetrySyncText = $"{sync.RetryCount:N0} retrying";
        SyncReviewText = $"{exhaustedCount:N0} need review";
    }

    private void ApplyRecovery(int recoverableCount)
    {
        CheckoutRecoveryText = recoverableCount == 1
            ? "1 checkout"
            : $"{recoverableCount:N0} checkouts";
    }

    private void ApplyConnectivity(ApiConnectivitySnapshot snapshot)
    {
        ConnectivityText = snapshot.Status switch
        {
            ApiConnectivityStatus.Online => "API online",
            ApiConnectivityStatus.Offline => "API offline",
            _ => "API status unknown"
        };
    }

    private void ApplyAttention(SyncStatusSnapshot sync, int recoverableCount)
    {
        var exhaustedCount = SyncReviewRequiredCount(sync);
        var workCount = sync.PendingCount + sync.RetryCount + exhaustedCount + recoverableCount;
        DashboardStatusText = _connectivityStateStore.Current.Status switch
        {
            ApiConnectivityStatus.Offline => "API OFFLINE",
            ApiConnectivityStatus.Unknown => "STATUS UNKNOWN",
            _ when workCount > 0 => "ACTION NEEDED",
            _ => "ALL SYSTEMS NORMAL"
        };
        DashboardStatusForeground = DashboardStatusText switch
        {
            "ALL SYSTEMS NORMAL" => "#FF027A48",
            "ACTION NEEDED" => "#FFB54708",
            "API OFFLINE" => "#FFB91C1C",
            _ => "#FF374151"
        };
        DashboardStatusBackground = DashboardStatusText switch
        {
            "ALL SYSTEMS NORMAL" => "#FFECFDF3",
            "ACTION NEEDED" => "#FFFFF7ED",
            "API OFFLINE" => "#FFFEE2E2",
            _ => "#FFE5E7EB"
        };

        if (recoverableCount > 0)
        {
            AttentionTitle = recoverableCount == 1
                ? "1 checkout needs recovery"
                : $"{recoverableCount:N0} checkouts need recovery";
            AttentionDetail = "Review approved payments before returning to checkout.";
            AttentionBackground = "#FFFEE2E2";
            return;
        }

        if (exhaustedCount > 0)
        {
            AttentionTitle = exhaustedCount == 1
                ? "1 sync item needs review"
                : $"{exhaustedCount:N0} sync items need review";
            AttentionDetail = "Open Status to review local sync records.";
            AttentionBackground = "#FFFEE2E2";
            return;
        }

        var pendingSyncCount = sync.PendingCount + sync.RetryCount;
        if (pendingSyncCount > 0)
        {
            AttentionTitle = pendingSyncCount == 1
                ? "1 sync item waiting"
                : $"{pendingSyncCount:N0} sync items waiting";
            AttentionDetail = ConnectivityText == "API online"
                ? "Sync is available from the Status screen."
                : "Sync will retry when API connectivity returns.";
            AttentionBackground = "#FFFFF7ED";
            return;
        }

        AttentionTitle = "No operations need attention";
        AttentionDetail = "Orders, sync queue, and recovery records are clear.";
        AttentionBackground = "#FFECFDF3";
    }

    private static int SyncReviewRequiredCount(SyncStatusSnapshot sync) =>
        sync.Items.Count(item => item.Status == SyncQueueStatus.Exhausted);

    private DateTimeOffset UtcNow()
    {
        var value = _clock.UtcNow;
        if (value.Offset != TimeSpan.Zero)
        {
            throw new InvalidOperationException("Dashboard clock must return UTC timestamps.");
        }

        return value;
    }

    partial void OnIsBusyChanged(bool value) => RefreshCommand.NotifyCanExecuteChanged();
    partial void OnErrorMessageChanged(string? value) => OnPropertyChanged(nameof(HasError));

    private void OnRecentOrdersChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        NotifyRecentOrderProperties();

    private void NotifyRecentOrderProperties()
    {
        OnPropertyChanged(nameof(HasRecentOrders));
        OnPropertyChanged(nameof(HasNoRecentOrders));
    }
}

public sealed class DashboardRecentOrderViewModel(Order order)
{
    public string OrderNumber { get; } = order.LocalOrderNumber;
    public string CreatedAtText { get; } = order.CreatedAtUtc.ToLocalTime().ToString("HH:mm");
    public string StatusText { get; } = order.Status.ToString();
    public string TotalText { get; } = $"{order.TotalAmount:N0} KRW";
}
