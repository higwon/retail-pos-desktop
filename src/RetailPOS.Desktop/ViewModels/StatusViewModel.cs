using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RetailPOS.Application.Persistence;
using RetailPOS.Application.Sync;

namespace RetailPOS.Desktop.ViewModels;

public sealed partial class StatusViewModel : ObservableObject
{
    private const int SnapshotCount = 50;
    private const int SyncBatchSize = 10;

    private readonly SyncStatusService _syncStatusService;
    private readonly OrderSyncService _orderSyncService;
    private readonly IOrderSyncClock _clock;

    public StatusViewModel(
        SyncStatusService syncStatusService,
        OrderSyncService orderSyncService,
        IOrderSyncClock clock)
    {
        _syncStatusService = syncStatusService;
        _orderSyncService = orderSyncService;
        _clock = clock;
        RefreshCommand = new AsyncRelayCommand(LoadAsync, () => !IsBusy);
        RunSyncCommand = new AsyncRelayCommand(RunSyncAsync, () => !IsBusy);
        Items.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasItems));
    }

    public ObservableCollection<SyncQueueItemViewModel> Items { get; } = [];
    public IAsyncRelayCommand RefreshCommand { get; }
    public IAsyncRelayCommand RunSyncCommand { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedItem))]
    private SyncQueueItemViewModel? _selectedItem;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusMessage = "Sync status has not been loaded yet.";

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string _lastCheckedText = "Not checked";

    [ObservableProperty]
    private int _pendingCount;

    [ObservableProperty]
    private int _retryCount;

    [ObservableProperty]
    private int _completedCount;

    [ObservableProperty]
    private int _reviewCount;

    public bool HasItems => Items.Count > 0;
    public bool HasSelectedItem => SelectedItem is not null;
    public string PendingCountText => $"{PendingCount:N0} items";
    public string RetryCountText => $"{RetryCount:N0} retrying";
    public string ReviewCountText => $"{ReviewCount:N0} review";
    public string CompletedCountText => $"{CompletedCount:N0} done";

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        await RunBusyAsync(async token =>
        {
            var snapshot = await _syncStatusService.GetSnapshotAsync(SnapshotCount, token);
            ApplySnapshot(snapshot);
            StatusMessage = Items.Count == 0
                ? "No local sync queue records."
                : $"{Items.Count:N0} local sync queue records loaded.";
        }, cancellationToken);
    }

    private async Task RunSyncAsync(CancellationToken cancellationToken = default)
    {
        await RunBusyAsync(async token =>
        {
            var result = await _orderSyncService.ProcessDueAsync(UtcNow(), SyncBatchSize, token);
            var snapshot = await _syncStatusService.GetSnapshotAsync(SnapshotCount, token);
            ApplySnapshot(snapshot);
            StatusMessage = $"Sync run completed. {result.CompletedCount:N0} completed, {result.RetriedCount:N0} retrying, {result.ExhaustedCount:N0} need review.";
        }, cancellationToken);
    }

    private async Task RunBusyAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken)
    {
        IsBusy = true;
        ErrorMessage = null;

        try
        {
            await operation(cancellationToken);
        }
        catch (Exception)
        {
            ErrorMessage = "Sync status could not be updated. Try again or review the terminal logs.";
        }
        finally
        {
            IsBusy = false;
            NotifyCommandStateChanged();
        }
    }

    private void ApplySnapshot(SyncStatusSnapshot snapshot)
    {
        PendingCount = snapshot.PendingCount;
        RetryCount = snapshot.RetryCount;
        CompletedCount = snapshot.CompletedCount;
        ReviewCount = snapshot.ReviewCount;
        LastCheckedText = snapshot.CheckedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");

        Items.Clear();
        foreach (var item in snapshot.Items.Select(item => new SyncQueueItemViewModel(item)))
        {
            Items.Add(item);
        }

        SelectedItem = Items.FirstOrDefault();
        NotifyCountProperties();
    }

    private DateTimeOffset UtcNow()
    {
        var value = _clock.UtcNow;
        if (value.Offset != TimeSpan.Zero)
        {
            throw new InvalidOperationException("Sync clock must return UTC timestamps.");
        }

        return value;
    }

    partial void OnIsBusyChanged(bool value)
    {
        NotifyCommandStateChanged();
    }

    partial void OnPendingCountChanged(int value) => OnPropertyChanged(nameof(PendingCountText));
    partial void OnRetryCountChanged(int value) => OnPropertyChanged(nameof(RetryCountText));
    partial void OnCompletedCountChanged(int value) => OnPropertyChanged(nameof(CompletedCountText));
    partial void OnReviewCountChanged(int value) => OnPropertyChanged(nameof(ReviewCountText));

    private void NotifyCommandStateChanged()
    {
        RefreshCommand.NotifyCanExecuteChanged();
        RunSyncCommand.NotifyCanExecuteChanged();
    }

    private void NotifyCountProperties()
    {
        OnPropertyChanged(nameof(PendingCountText));
        OnPropertyChanged(nameof(RetryCountText));
        OnPropertyChanged(nameof(CompletedCountText));
        OnPropertyChanged(nameof(ReviewCountText));
        OnPropertyChanged(nameof(HasSelectedItem));
    }
}

public sealed class SyncQueueItemViewModel(SyncStatusItem item)
{
    public Guid Id { get; } = item.Id;
    public string ItemType { get; } = item.ItemType;
    public Guid AggregateId { get; } = item.AggregateId;
    public string ReferenceKey { get; } = item.ReferenceKey ?? "-";
    public SyncQueueStatus Status { get; } = item.Status;
    public int RetryCount { get; } = item.RetryCount;
    public DateTimeOffset NextAttemptAtLocal { get; } = item.NextAttemptAtUtc.ToLocalTime();
    public DateTimeOffset CreatedAtLocal { get; } = item.CreatedAtUtc.ToLocalTime();
    public DateTimeOffset UpdatedAtLocal { get; } = item.UpdatedAtUtc.ToLocalTime();
    public string LastErrorSummary { get; } = SafeErrorSummary(item.LastErrorSummary);

    public string Title => $"{ItemType} {AggregateId.ToString("N")[..8].ToUpperInvariant()}";
    public string StatusText => Status switch
    {
        SyncQueueStatus.Pending when RetryCount > 0 => "Retrying",
        SyncQueueStatus.Pending => "Pending",
        SyncQueueStatus.Completed => "Completed",
        SyncQueueStatus.Resolved => "Resolved",
        SyncQueueStatus.Exhausted => "Needs review",
        _ => Status.ToString()
    };
    public string CreatedAtText => CreatedAtLocal.ToString("yyyy-MM-dd HH:mm");
    public string UpdatedAtText => UpdatedAtLocal.ToString("yyyy-MM-dd HH:mm");
    public string NextAttemptText => Status == SyncQueueStatus.Pending
        ? NextAttemptAtLocal.ToString("yyyy-MM-dd HH:mm")
        : "-";
    public string RetryCountText => RetryCount.ToString("N0");
    public string ReferenceKeyShort => ReferenceKey.Length <= 24 ? ReferenceKey : $"{ReferenceKey[..24]}...";

    private static string SafeErrorSummary(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "None";
        }

        var lower = value.ToLowerInvariant();
        return lower.Contains("password", StringComparison.Ordinal) ||
            lower.Contains("secret", StringComparison.Ordinal) ||
            lower.Contains("token", StringComparison.Ordinal)
            ? "Sync attempt failed. Review terminal logs."
            : value;
    }
}
