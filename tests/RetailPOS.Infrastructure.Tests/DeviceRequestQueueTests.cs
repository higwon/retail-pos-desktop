using RetailPOS.Infrastructure.Devices;

namespace RetailPOS.Infrastructure.Tests;

public sealed class DeviceRequestQueueTests
{
    [Fact]
    public async Task CompleteIsExactlyOnceAndContinuationRunsAsynchronously()
    {
        using var queue = new DeviceRequestQueue<string, string>("Printer", TimeProvider.System);
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
    }

    [Fact]
    public async Task CancellationWinsAndLateResponseIsRejected()
    {
        using var cancellation = new CancellationTokenSource();
        using var queue = new DeviceRequestQueue<string, string>("Card", TimeProvider.System);
        var task = queue.BeginAsync("attempt-1", "amount=1000", TimeSpan.FromMinutes(1), cancellation.Token);
        var id = queue.Pending!.RequestId;

        cancellation.Cancel();
        var completed = await task;

        Assert.Equal(DeviceRequestState.Cancelled, completed.State);
        Assert.False(queue.TryComplete(id, "Approve"));
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
        var queue = new DeviceRequestQueue<string, string>("Printer", TimeProvider.System);
        var task = queue.BeginAsync("receipt", "payload", TimeSpan.FromMinutes(1));
        queue.Dispose();
        Assert.Equal(DeviceRequestState.Disposed, (await task).State);
        Assert.Throws<ObjectDisposedException>(BeginAfterDispose);

        void BeginAfterDispose() =>
            _ = queue.BeginAsync("later", "payload", TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task TimeoutTerminatesRequestAndRejectsLaterCompletion()
    {
        using var queue = new DeviceRequestQueue<string, string>("Printer", TimeProvider.System);
        var task = queue.BeginAsync("receipt", "payload", TimeSpan.FromMilliseconds(30));
        var id = queue.Pending!.RequestId;
        var completed = await task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(DeviceRequestState.TimedOut, completed.State);
        Assert.False(queue.TryComplete(id, "Printed"));
    }

    [Fact]
    public async Task DisconnectTerminatesOnlyMatchingPendingRequest()
    {
        using var queue = new DeviceRequestQueue<string, string>("Card", TimeProvider.System);
        var task = queue.BeginAsync("attempt", "payload", TimeSpan.FromMinutes(1));
        Assert.False(queue.TryDisconnect(Guid.NewGuid()));
        Assert.True(queue.TryDisconnect(queue.Pending!.RequestId));
        Assert.Equal(DeviceRequestState.Disconnected, (await task).State);
    }
}
