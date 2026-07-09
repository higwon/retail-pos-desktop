using RetailPOS.Application.Sync;

namespace RetailPOS.Desktop.Sync;

public interface IBackgroundOrderSyncRunner
{
    Task<OrderSyncRunResult> RunAsync(
        int batchSize,
        CancellationToken cancellationToken = default);
}

public sealed class BackgroundOrderSyncRunner(
    OrderSyncService orderSyncService,
    IOrderSyncClock clock) : IBackgroundOrderSyncRunner
{
    public Task<OrderSyncRunResult> RunAsync(
        int batchSize,
        CancellationToken cancellationToken = default) =>
        orderSyncService.ProcessDueAsync(clock.UtcNow, batchSize, cancellationToken);
}
