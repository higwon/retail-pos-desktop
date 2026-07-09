namespace RetailPOS.Desktop.Sync;

public enum ApiConnectivityStatus
{
    Unknown,
    Online,
    Offline
}

public sealed record ApiConnectivitySnapshot(
    ApiConnectivityStatus Status,
    DateTimeOffset CheckedAtUtc,
    string? FailureKind);

public interface IApiConnectivityStateStore
{
    ApiConnectivitySnapshot Current { get; }
    ApiConnectivitySnapshot Update(ApiConnectivitySnapshot snapshot);
}

public sealed class ApiConnectivityStateStore : IApiConnectivityStateStore
{
    private readonly object _gate = new();
    private ApiConnectivitySnapshot _current = new(ApiConnectivityStatus.Unknown, DateTimeOffset.MinValue, null);

    public ApiConnectivitySnapshot Current
    {
        get
        {
            lock (_gate)
            {
                return _current;
            }
        }
    }

    public ApiConnectivitySnapshot Update(ApiConnectivitySnapshot snapshot)
    {
        lock (_gate)
        {
            _current = snapshot;
            return _current;
        }
    }
}
