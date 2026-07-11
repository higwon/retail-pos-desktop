using System.Text.Json;
using System.Net.Http;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging.Abstractions;
using RetailPOS.Application.Orders;
using RetailPOS.Application.Persistence;
using RetailPOS.Application.Sync;
using RetailPOS.Desktop.Sync;
using RetailPOS.Desktop.ViewModels;
using System.Diagnostics;

namespace RetailPOS.Desktop.Tests;

public sealed class StatusViewModelTests
{
    private static readonly DateTimeOffset NowUtc = new(2026, 7, 9, 2, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task LoadAsync_ShowsCurrentSyncQueueStatus()
    {
        var queue = new RecordingSyncQueueRepository(
            QueueItem(SyncQueueStatus.Pending, retryCount: 0),
            QueueItem(SyncQueueStatus.Pending, retryCount: 2),
            QueueItem(SyncQueueStatus.Completed, retryCount: 0),
            QueueItem(SyncQueueStatus.Exhausted, retryCount: 5, lastError: "timeout"));
        var viewModel = ViewModel(queue);

        await viewModel.LoadAsync();

        Assert.Equal(4, viewModel.Items.Count);
        Assert.Equal(1, viewModel.PendingCount);
        Assert.Equal(1, viewModel.RetryCount);
        Assert.Equal(1, viewModel.CompletedCount);
        Assert.Equal(1, viewModel.ReviewCount);
        Assert.NotNull(viewModel.SelectedItem);
        Assert.Contains("records loaded", viewModel.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RefreshCommand_ReloadsQueueStatus()
    {
        var queue = new RecordingSyncQueueRepository();
        var viewModel = ViewModel(queue);

        await viewModel.RefreshCommand.ExecuteAsync(null);
        Assert.Empty(viewModel.Items);

        queue.Items.Add(QueueItem(SyncQueueStatus.Pending, retryCount: 0));
        await viewModel.RefreshCommand.ExecuteAsync(null);

        Assert.Single(viewModel.Items);
        Assert.Equal("1 items", viewModel.PendingCountText);
    }

    [Fact]
    public async Task RunSyncCommand_ProcessesDueOrdersAndRefreshesStatus()
    {
        var queue = new RecordingSyncQueueRepository(QueueItem(SyncQueueStatus.Pending, retryCount: 0));
        var client = new RecordingOrderUploadClient();
        var viewModel = ViewModel(queue, client);

        await viewModel.RunSyncCommand.ExecuteAsync(null);

        Assert.Single(client.Payloads);
        Assert.Equal(SyncQueueStatus.Completed, Assert.Single(queue.Items).Status);
        Assert.Equal(0, viewModel.PendingCount);
        Assert.Equal(1, viewModel.CompletedCount);
        Assert.Contains("1 completed", viewModel.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RunSyncCommand_ShowsSafeMessageWhenSyncFails()
    {
        var queue = new RecordingSyncQueueRepository(QueueItem(SyncQueueStatus.Pending, retryCount: 0));
        var viewModel = ViewModel(queue, new ThrowingOrderUploadClient());

        await viewModel.RunSyncCommand.ExecuteAsync(null);

        Assert.Null(viewModel.ErrorMessage);
        Assert.Equal(1, viewModel.RetryCount);
        Assert.Equal("Sync attempt failed. Review terminal logs.", Assert.Single(viewModel.Items).LastErrorSummary);
        Assert.Contains("retrying", viewModel.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LoadAsync_DoesNotReloadWhileBusy()
    {
        var queue = new RecordingSyncQueueRepository(QueueItem(SyncQueueStatus.Pending, retryCount: 0));
        var viewModel = ViewModel(queue);
        viewModel.IsBusy = true;

        await viewModel.LoadAsync();

        Assert.Empty(viewModel.Items);
        Assert.Equal("Sync status has not been loaded yet.", viewModel.StatusMessage);
    }

    [Fact]
    public async Task LargeSyncHistory_LoadsBoundedStatusSummary()
    {
        var queue = new RecordingSyncQueueRepository(
            Enumerable.Range(1, 2000)
                .Select(index => QueueItem(
                    index % 10 == 0 ? SyncQueueStatus.Exhausted : SyncQueueStatus.Pending,
                    index % 4))
                .ToArray());
        var viewModel = ViewModel(queue);

        var stopwatch = Stopwatch.StartNew();
        await viewModel.LoadAsync();
        stopwatch.Stop();

        Assert.Equal(50, viewModel.Items.Count);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5),
            $"Status load exceeded baseline ceiling: {stopwatch.Elapsed}.");
    }

    [Fact]
    public async Task SyncStatusMessage_RefreshesStatusAutomatically()
    {
        var messenger = new WeakReferenceMessenger();
        var queue = new RecordingSyncQueueRepository();
        var viewModel = ViewModel(queue, messenger: messenger);
        await viewModel.LoadAsync();

        queue.Items.Add(QueueItem(SyncQueueStatus.Pending, retryCount: 0));
        messenger.Send(new SyncStatusChangedMessage("Sync status updated by background work."));

        await WaitUntilAsync(() => viewModel.Items.Count == 1);
        Assert.Equal(1, viewModel.PendingCount);
        Assert.Equal("Sync status updated by background work.", viewModel.StatusMessage);
    }

    [Fact]
    public async Task OrderSyncRunCompletedMessage_RefreshesStatusAutomatically()
    {
        var messenger = new WeakReferenceMessenger();
        var queue = new RecordingSyncQueueRepository();
        var viewModel = ViewModel(queue, messenger: messenger);
        await viewModel.LoadAsync();

        queue.Items.Add(QueueItem(SyncQueueStatus.Exhausted, retryCount: 5, lastError: "timeout"));
        messenger.Send(new OrderSyncRunCompletedMessage(new OrderSyncRunResult(1, 0, 0, 1, 0)));

        await WaitUntilAsync(() => viewModel.ReviewCount == 1);
        Assert.Single(viewModel.Items);
        Assert.Contains("need review", viewModel.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }


    [Fact]
    public async Task Dispose_UnregistersFromSyncStatusMessages()
    {
        var messenger = new WeakReferenceMessenger();
        var queue = new RecordingSyncQueueRepository();
        var viewModel = ViewModel(queue, messenger: messenger);
        await viewModel.LoadAsync();

        viewModel.Dispose();
        queue.Items.Add(QueueItem(SyncQueueStatus.Pending, retryCount: 0));
        messenger.Send(new SyncStatusChangedMessage("Should not refresh."));
        await Task.Delay(50);

        Assert.Empty(viewModel.Items);
        Assert.Equal("No local sync queue records.", viewModel.StatusMessage);
    }

    private static StatusViewModel ViewModel(
        RecordingSyncQueueRepository queue,
        IOrderUploadClient? client = null,
        IMessenger? messenger = null)
    {
        var clock = new StubOrderSyncClock(NowUtc);
        return new StatusViewModel(
            new SyncStatusService(queue, clock),
            new OrderSyncService(queue, client ?? new RecordingOrderUploadClient(), clock, NullLogger<OrderSyncService>.Instance),
            clock,
            messenger ?? new WeakReferenceMessenger());
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(25);
        }

        Assert.True(condition());
    }

    private static SyncQueueRecord QueueItem(
        SyncQueueStatus status,
        int retryCount,
        string? lastError = null) => new(
        Guid.NewGuid(),
        "Order",
        Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001"),
        JsonSerializer.Serialize(Payload(), new JsonSerializerOptions(JsonSerializerDefaults.Web)),
        "10000000000000000000000000000001:20000000000000000000000000000001:bbbbbbbb000000000000000000000001",
        status,
        retryCount,
        NowUtc,
        lastError,
        NowUtc.AddMinutes(-5),
        NowUtc.AddMinutes(-1));

    private static OrderUploadPayload Payload() => new(
        OrderUploadPayload.CurrentSchemaVersion,
        Guid.Parse("10000000-0000-0000-0000-000000000001"),
        Guid.Parse("20000000-0000-0000-0000-000000000001"),
        Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001"),
        "10000000000000000000000000000001:20000000000000000000000000000001:bbbbbbbb000000000000000000000001",
        "POS-20260709-000001",
        new DateOnly(2026, 7, 9),
        Guid.Parse("30000000-0000-0000-0000-000000000001"),
        3600m,
        200m,
        3400m,
        NowUtc.AddMinutes(-10),
        [
            new OrderUploadLinePayload(
                Guid.Parse("11111111-0000-0000-0000-000000000001"),
                "Cola",
                1800m,
                2,
                200m,
                3400m)
        ],
        [
            new OrderUploadPaymentPayload("Card", 3400m, "APP-001", "TX-001", NowUtc.AddMinutes(-9))
        ]);

    private sealed class RecordingSyncQueueRepository(params SyncQueueRecord[] items) : ISyncQueueRepository
    {
        public List<SyncQueueRecord> Items { get; } = items.ToList();

        public Task EnqueueAsync(SyncQueueRecord item, CancellationToken cancellationToken = default)
        {
            Items.Add(item);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<SyncQueueRecord>> GetDuePendingAsync(
            DateTimeOffset asOfUtc,
            int count,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<SyncQueueRecord>>(Items
                .Where(item => item.Status == SyncQueueStatus.Pending && item.NextAttemptAtUtc <= asOfUtc)
                .Take(count)
                .ToArray());

        public Task<IReadOnlyList<SyncQueueRecord>> GetRecentAsync(
            int count,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<SyncQueueRecord>>(Items.Take(count).ToArray());

        public Task<bool> ExistsByReferenceKeyAsync(
            string referenceKey,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Items.Any(item => item.ReferenceKey == referenceKey));

        public Task UpdateRetryAsync(
            Guid id,
            int retryCount,
            DateTimeOffset nextAttemptAtUtc,
            string? lastErrorSummary,
            DateTimeOffset updatedAtUtc,
            CancellationToken cancellationToken = default)
        {
            Replace(id, item => item with
            {
                RetryCount = retryCount,
                NextAttemptAtUtc = nextAttemptAtUtc,
                LastErrorSummary = lastErrorSummary,
                UpdatedAtUtc = updatedAtUtc
            });
            return Task.CompletedTask;
        }

        public Task MarkCompletedAsync(
            Guid id,
            DateTimeOffset completedAtUtc,
            CancellationToken cancellationToken = default)
        {
            Replace(id, item => item with
            {
                Status = SyncQueueStatus.Completed,
                UpdatedAtUtc = completedAtUtc
            });
            return Task.CompletedTask;
        }

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
            CancellationToken cancellationToken = default)
        {
            Replace(id, item => item with
            {
                Status = SyncQueueStatus.Exhausted,
                RetryCount = retryCount,
                LastErrorSummary = lastErrorSummary,
                UpdatedAtUtc = exhaustedAtUtc
            });
            return Task.CompletedTask;
        }

        private void Replace(Guid id, Func<SyncQueueRecord, SyncQueueRecord> update)
        {
            var index = Items.FindIndex(item => item.Id == id);
            Items[index] = update(Items[index]);
        }
    }

    private sealed class RecordingOrderUploadClient : IOrderUploadClient
    {
        public List<OrderUploadPayload> Payloads { get; } = [];

        public Task<OrderUploadResult> UploadAsync(
            OrderUploadPayload payload,
            CancellationToken cancellationToken = default)
        {
            Payloads.Add(payload);
            return Task.FromResult(new OrderUploadResult(Guid.NewGuid(), "SYNCED-POS-20260709-000001", "Synced"));
        }
    }

    private sealed class ThrowingOrderUploadClient : IOrderUploadClient
    {
        public Task<OrderUploadResult> UploadAsync(
            OrderUploadPayload payload,
            CancellationToken cancellationToken = default) =>
            throw new HttpRequestException("payment gateway password=secret");
    }

    private sealed class StubOrderSyncClock(DateTimeOffset utcNow) : IOrderSyncClock
    {
        public DateTimeOffset UtcNow => utcNow;
    }
}
