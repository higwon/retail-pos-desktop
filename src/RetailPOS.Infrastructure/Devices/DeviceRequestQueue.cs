namespace RetailPOS.Infrastructure.Devices;

public enum DeviceRequestState
{
    Pending,
    Completed,
    Cancelled,
    TimedOut,
    Disconnected,
    Disposed
}

public sealed record DeviceRequest<TPayload, TResult>(
    Guid RequestId,
    string Device,
    string BusinessIdentity,
    DateTimeOffset ReceivedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    DeviceRequestState State,
    TPayload Payload,
    TResult? Result);

public sealed class DeviceRequestBusyException(string device)
    : InvalidOperationException($"{device} already has an active request.");

public sealed class DeviceRequestQueue<TPayload, TResult> : IDisposable
{
    private readonly object _sync = new();
    private readonly string _device;
    private readonly TimeProvider _timeProvider;
    private readonly int _historyLimit;
    private readonly List<DeviceRequest<TPayload, TResult>> _history = [];
    private ActiveRequest? _active;
    private bool _disposed;

    public DeviceRequestQueue(string device, TimeProvider timeProvider, int historyLimit = 20)
    {
        if (string.IsNullOrWhiteSpace(device)) throw new ArgumentException("Device name is required.", nameof(device));
        if (historyLimit is < 1 or > 50) throw new ArgumentOutOfRangeException(nameof(historyLimit));
        _device = device;
        _timeProvider = timeProvider;
        _historyLimit = historyLimit;
    }

    public event EventHandler? Changed;
    public DeviceRequest<TPayload, TResult>? Pending { get { lock (_sync) return _active?.Snapshot; } }
    public IReadOnlyList<DeviceRequest<TPayload, TResult>> Recent { get { lock (_sync) return _history.ToArray(); } }

    public Task<DeviceRequest<TPayload, TResult>> BeginAsync(
        string businessIdentity,
        TPayload payload,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(businessIdentity)) throw new ArgumentException("Business identity is required.", nameof(businessIdentity));
        if (payload is null) throw new ArgumentNullException(nameof(payload));
        if (timeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(timeout));

        ActiveRequest active;
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_active is not null) throw new DeviceRequestBusyException(_device);
            var snapshot = new DeviceRequest<TPayload, TResult>(Guid.NewGuid(), _device, businessIdentity,
                _timeProvider.GetUtcNow(), null, DeviceRequestState.Pending, payload, default);
            active = new ActiveRequest(snapshot);
            _active = active;
        }

        var registration = cancellationToken.Register(() =>
            TryFinish(active.Snapshot.RequestId, DeviceRequestState.Cancelled, default));
        active.CancellationRegistration = registration;
        if (active.Completion.Task.IsCompleted) registration.Dispose();
        _ = TimeoutAsync(active.Snapshot.RequestId, timeout);
        RaiseChanged();
        return active.Completion.Task;
    }

    public bool TryComplete(Guid requestId, TResult result) =>
        TryFinish(requestId, DeviceRequestState.Completed, result);

    public bool TryDisconnect(Guid requestId) =>
        TryFinish(requestId, DeviceRequestState.Disconnected, default);

    private async Task TimeoutAsync(Guid requestId, TimeSpan timeout)
    {
        await Task.Delay(timeout, _timeProvider, CancellationToken.None);
        TryFinish(requestId, DeviceRequestState.TimedOut, default);
    }

    private bool TryFinish(Guid requestId, DeviceRequestState state, TResult? result)
    {
        ActiveRequest? active;
        DeviceRequest<TPayload, TResult> completed;
        lock (_sync)
        {
            active = _active;
            if (active is null || active.Snapshot.RequestId != requestId || active.Snapshot.State != DeviceRequestState.Pending)
            {
                return false;
            }

            completed = active.Snapshot with
            {
                State = state,
                CompletedAtUtc = _timeProvider.GetUtcNow(),
                Result = result
            };
            _active = null;
            _history.Insert(0, completed);
            if (_history.Count > _historyLimit) _history.RemoveRange(_historyLimit, _history.Count - _historyLimit);
        }

        active.CancellationRegistration.Dispose();
        active.Completion.TrySetResult(completed);
        RaiseChanged();
        return true;
    }

    private void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);

    public void Dispose()
    {
        Guid? requestId;
        lock (_sync)
        {
            if (_disposed) return;
            _disposed = true;
            requestId = _active?.Snapshot.RequestId;
        }
        if (requestId is not null) TryFinish(requestId.Value, DeviceRequestState.Disposed, default);
    }

    private sealed class ActiveRequest(DeviceRequest<TPayload, TResult> snapshot)
    {
        public DeviceRequest<TPayload, TResult> Snapshot { get; } = snapshot;
        public TaskCompletionSource<DeviceRequest<TPayload, TResult>> Completion { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public CancellationTokenRegistration CancellationRegistration { get; set; }
    }
}
