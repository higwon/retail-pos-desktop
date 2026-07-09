using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace RetailPOS.Desktop.Sync;

public sealed class ApiConnectivityMonitor(
    IApiConnectivityClient connectivityClient,
    IApiConnectivityStateStore stateStore,
    BackgroundOrderSyncScheduler syncScheduler,
    IOptions<ApiConnectivityMonitorOptions> monitorOptions,
    IOptions<BackgroundOrderSyncOptions> syncOptions,
    ILogger<ApiConnectivityMonitor> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var configured = Normalize(monitorOptions.Value);
        if (!configured.Enabled)
        {
            logger.LogInformation("API connectivity monitor is disabled.");
            return;
        }

        logger.LogInformation(
            "API connectivity monitor started. {InitialDelaySeconds} {IntervalSeconds} {TriggerSyncOnReconnect}",
            configured.InitialDelaySeconds,
            configured.IntervalSeconds,
            configured.TriggerSyncOnReconnect);

        try
        {
            if (configured.InitialDelaySeconds > 0)
            {
                await Task.Delay(TimeSpan.FromSeconds(configured.InitialDelaySeconds), stoppingToken);
            }

            await CheckOnceAsync(stoppingToken);

            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(configured.IntervalSeconds));
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await CheckOnceAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            logger.LogInformation("API connectivity monitor stopped.");
        }
    }

    public async Task<ApiConnectivitySnapshot> CheckOnceAsync(CancellationToken cancellationToken = default)
    {
        var previous = stateStore.Current;
        var current = await connectivityClient.CheckAsync(cancellationToken);
        stateStore.Update(current);
        LogStateChange(previous, current);

        if (ShouldTriggerSync(previous, current))
        {
            var batchSize = Math.Max(1, syncOptions.Value.BatchSize);
            logger.LogInformation(
                "API connectivity recovered; triggering one background sync run. {BatchSize}",
                batchSize);
            await syncScheduler.RunOnceAsync(batchSize, cancellationToken);
        }

        return current;
    }

    private bool ShouldTriggerSync(ApiConnectivitySnapshot previous, ApiConnectivitySnapshot current) =>
        monitorOptions.Value.TriggerSyncOnReconnect &&
        syncOptions.Value.Enabled &&
        current.Status == ApiConnectivityStatus.Online &&
        previous.Status == ApiConnectivityStatus.Offline;

    private void LogStateChange(ApiConnectivitySnapshot previous, ApiConnectivitySnapshot current)
    {
        if (previous.Status == current.Status)
        {
            return;
        }

        if (current.Status == ApiConnectivityStatus.Online)
        {
            logger.LogInformation(
                "API connectivity is online. {PreviousStatus} {CheckedAtUtc}",
                previous.Status,
                current.CheckedAtUtc);
            return;
        }

        logger.LogWarning(
            "API connectivity is offline. {PreviousStatus} {CheckedAtUtc} {FailureKind}",
            previous.Status,
            current.CheckedAtUtc,
            current.FailureKind);
    }

    private static ApiConnectivityMonitorOptions Normalize(ApiConnectivityMonitorOptions value) => new()
    {
        Enabled = value.Enabled,
        InitialDelaySeconds = Math.Max(0, value.InitialDelaySeconds),
        IntervalSeconds = Math.Max(5, value.IntervalSeconds),
        TriggerSyncOnReconnect = value.TriggerSyncOnReconnect
    };
}
