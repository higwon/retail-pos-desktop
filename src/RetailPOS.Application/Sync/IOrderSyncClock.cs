namespace RetailPOS.Application.Sync;

public interface IOrderSyncClock
{
    DateTimeOffset UtcNow { get; }
}

public sealed class SystemOrderSyncClock : IOrderSyncClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
