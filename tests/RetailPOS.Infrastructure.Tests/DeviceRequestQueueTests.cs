using RetailPOS.Infrastructure.Devices;

namespace RetailPOS.Infrastructure.Tests;

public sealed class DeviceRequestQueueTests
{
    [Fact]
    public async Task CompleteIsExactlyOnceAndContinuationRunsAsynchronously()
    {
        var timeProvider = new TrackingTimeProvider();
        using var queue = new DeviceRequestQueue<string, string>("Printer", timeProvider);
        var task = queue.BeginAsync("receipt-1", "immutable payload", TimeSpan.FromMinutes(1));
        var requestId = queue.Pending!.RequestId;
        var continuationRan = false;
        var continuation = task.ContinueWith(_ => continuationRan = true, TaskScheduler.Default);

        Assert.True(queue.TryComplete(requestId, "Printed"));
        Assert.False(queue.TryComplete(requestId, "Failed"));
        var completed = await task;
        await continuation;

        Assert.True(continuationRan);
        Assert.Equal(DeviceRequestState.Completed, completed.State);
        Assert.Equal("Printed", completed.Result);
        Assert.Single(queue.Recent);
        await WaitForTimersAsync(timeProvider, 0);
    }

    [Fact]
    public async Task CancellationWinsAndLateResponseIsRejected()
    {
        var timeProvider = new TrackingTimeProvider();
        using var cancellation = new CancellationTokenSource();
        using var queue = new DeviceRequestQueue<string, string>("Card", timeProvider);
        var task = queue.BeginAsync("attempt-1", "amount=1000", TimeSpan.FromMinutes(1), cancellation.Token);
        var id = queue.Pending!.RequestId;

        cancellation.Cancel();
        var completed = await task;

        Assert.Equal(DeviceRequestState.Cancelled, completed.State);
        Assert.False(queue.TryComplete(id, "Approve"));
        await WaitForTimersAsync(timeProvider, 0);
    }

    [Fact]
    public async Task SingleActivePolicyPreservesArrivalAndBoundsRecentHistory()
    {
        using var queue = new DeviceRequestQueue<int, string>("Printer", TimeProvider.System, historyLimit: 2);
        var first = queue.BeginAsync("one", 1, TimeSpan.FromMinutes(1));
        Assert.Throws<DeviceRequestBusyException>(BeginBlockedRequest);
        queue.TryComplete(queue.Pending!.RequestId, "ok"); await first;
        for (var value = 2; value <= 3; value++)
        {
            var task = queue.BeginAsync(value.ToString(), value, TimeSpan.FromMinutes(1));
            queue.TryComplete(queue.Pending!.RequestId, "ok"); await task;
        }

        Assert.Equal([3, 2], queue.Recent.Select(item => item.Payload));
        Assert.NotEqual(queue.Recent[0].RequestId, queue.Recent[1].RequestId);

        void BeginBlockedRequest() =>
            _ = queue.BeginAsync("blocked", 2, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task DisposeTerminatesPendingRequest()
    {
        var timeProvider = new TrackingTimeProvider();
        var queue = new DeviceRequestQueue<string, string>("Printer", timeProvider);
        var task = queue.BeginAsync("receipt", "payload", TimeSpan.FromMinutes(1));
        queue.Dispose();
        Assert.Equal(DeviceRequestState.Disposed, (await task).State);
        Assert.Throws<ObjectDisposedException>(BeginAfterDispose);
        await WaitForTimersAsync(timeProvider, 0);

        void BeginAfterDispose() =>
            _ = queue.BeginAsync("later", "payload", TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task TimeoutTerminatesRequestAndRejectsLaterCompletion()
    {
        var timeProvider = new TrackingTimeProvider();
        using var queue = new DeviceRequestQueue<string, string>("Printer", timeProvider);
        var task = queue.BeginAsync("receipt", "payload", TimeSpan.FromMinutes(1));
        var id = queue.Pending!.RequestId;
        timeProvider.FireTimers();
        var completed = await task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(DeviceRequestState.TimedOut, completed.State);
        Assert.False(queue.TryComplete(id, "Printed"));
        await WaitForTimersAsync(timeProvider, 0);
    }

    [Fact]
    public async Task DisconnectTerminatesOnlyMatchingPendingRequest()
    {
        var timeProvider = new TrackingTimeProvider();
        using var queue = new DeviceRequestQueue<string, string>("Card", timeProvider);
        var task = queue.BeginAsync("attempt", "payload", TimeSpan.FromMinutes(1));
        Assert.False(queue.TryDisconnect(Guid.NewGuid()));
        Assert.True(queue.TryDisconnect(queue.Pending!.RequestId));
        Assert.Equal(DeviceRequestState.Disconnected, (await task).State);
        await WaitForTimersAsync(timeProvider, 0);
    }

    private static async Task WaitForTimersAsync(TrackingTimeProvider provider, int expected)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        while (provider.ActiveTimers != expected)
        {
            await Task.Delay(10, timeout.Token);
        }
    }

    private sealed class TrackingTimeProvider : TimeProvider
    {
        private readonly List<Action> _timerCallbacks = [];
        private readonly object _timerSync = new();
        private int _activeTimers;
        public int ActiveTimers => Volatile.Read(ref _activeTimers);

        public void FireTimers()
        {
            Action[] callbacks;
            lock (_timerSync) callbacks = [.. _timerCallbacks];
            foreach (var callback in callbacks) callback();
        }

        public override ITimer CreateTimer(
            TimerCallback callback,
            object? state,
            TimeSpan dueTime,
            TimeSpan period)
        {
            Interlocked.Increment(ref _activeTimers);
            lock (_timerSync) _timerCallbacks.Add(() => callback(state));
            return new TrackingTimer(
                TimeProvider.System.CreateTimer(callback, state, dueTime, period),
                () => Interlocked.Decrement(ref _activeTimers));
        }

        private sealed class TrackingTimer(ITimer inner, Action onDispose) : ITimer
        {
            private int _disposed;
            public bool Change(TimeSpan dueTime, TimeSpan period) => inner.Change(dueTime, period);
            public void Dispose()
            {
                if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
                inner.Dispose();
                onDispose();
            }
            public async ValueTask DisposeAsync()
            {
                if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
                await inner.DisposeAsync();
                onDispose();
            }
        }
    }
}
