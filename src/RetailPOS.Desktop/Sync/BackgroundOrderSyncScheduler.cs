using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace RetailPOS.Desktop.Sync;

public sealed class BackgroundOrderSyncScheduler(
    IServiceScopeFactory scopeFactory,
    IOptions<BackgroundOrderSyncOptions> options,
    ILogger<BackgroundOrderSyncScheduler> logger) : BackgroundService
{
    private readonly SemaphoreSlim _runLock = new(1, 1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var configured = Normalize(options.Value);
        if (!configured.Enabled)
        {
            logger.LogInformation("Background order sync scheduler is disabled.");
            return;
        }

        logger.LogInformation(
            "Background order sync scheduler started. {InitialDelaySeconds} {IntervalSeconds} {BatchSize}",
            configured.InitialDelaySeconds,
            configured.IntervalSeconds,
            configured.BatchSize);

        try
        {
            if (configured.InitialDelaySeconds > 0)
            {
                await Task.Delay(TimeSpan.FromSeconds(configured.InitialDelaySeconds), stoppingToken);
            }

            await RunOnceAsync(configured.BatchSize, stoppingToken);

            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(configured.IntervalSeconds));
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await RunOnceAsync(configured.BatchSize, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            logger.LogInformation("Background order sync scheduler stopped.");
        }
    }

    public async Task<bool> RunOnceAsync(
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        if (!await _runLock.WaitAsync(0, cancellationToken))
        {
            logger.LogInformation("Background order sync run skipped because another run is already active.");
            return false;
        }

        try
        {
            using var scope = scopeFactory.CreateScope();
            var runner = scope.ServiceProvider.GetRequiredService<IBackgroundOrderSyncRunner>();
            var result = await runner.RunAsync(batchSize, cancellationToken);
            logger.LogInformation(
                "Background order sync run completed. {ProcessedCount} {CompletedCount} {RetriedCount} {ExhaustedCount} {SkippedCount}",
                result.ProcessedCount,
                result.CompletedCount,
                result.RetriedCount,
                result.ExhaustedCount,
                result.SkippedCount);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Background order sync run failed.");
            return true;
        }
        finally
        {
            _runLock.Release();
        }
    }

    private static BackgroundOrderSyncOptions Normalize(BackgroundOrderSyncOptions value) => new()
    {
        Enabled = value.Enabled,
        InitialDelaySeconds = Math.Max(0, value.InitialDelaySeconds),
        IntervalSeconds = Math.Max(1, value.IntervalSeconds),
        BatchSize = Math.Max(1, value.BatchSize)
    };
}
