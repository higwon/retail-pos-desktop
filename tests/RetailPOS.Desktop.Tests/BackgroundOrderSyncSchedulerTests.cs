using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RetailPOS.Application.Sync;
using RetailPOS.Desktop.Sync;

namespace RetailPOS.Desktop.Tests;

public sealed class BackgroundOrderSyncSchedulerTests
{
    [Fact]
    public async Task RunOnceAsync_ProcessesDueOrdersThroughScopedRunner()
    {
        var runner = new RecordingRunner();
        var scheduler = Scheduler(runner);

        var ran = await scheduler.RunOnceAsync(batchSize: 10);

        Assert.True(ran);
        Assert.Equal([10], runner.BatchSizes);
    }

    [Fact]
    public async Task RunOnceAsync_SkipsOverlappingRun()
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var runner = new BlockingRunner(release.Task);
        var scheduler = Scheduler(runner);

        var firstRun = scheduler.RunOnceAsync(batchSize: 10);
        await runner.Started.Task;
        var secondRun = await scheduler.RunOnceAsync(batchSize: 10);
        release.SetResult();
        var firstResult = await firstRun;

        Assert.True(firstResult);
        Assert.False(secondRun);
        Assert.Equal(1, runner.RunCount);
    }

    [Fact]
    public async Task RunOnceAsync_CatchesRunnerFailureSoSchedulerCanContinue()
    {
        var runner = new ThrowingRunner();
        var logger = new RecordingLogger<BackgroundOrderSyncScheduler>();
        var scheduler = Scheduler(runner, logger);

        var ran = await scheduler.RunOnceAsync(batchSize: 10);

        Assert.True(ran);
        Assert.Contains(logger.Messages, message => message.Contains("failed", StringComparison.OrdinalIgnoreCase));
    }

    private static BackgroundOrderSyncScheduler Scheduler(
        IBackgroundOrderSyncRunner runner,
        ILogger<BackgroundOrderSyncScheduler>? logger = null)
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => runner);
        return new BackgroundOrderSyncScheduler(
            services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new BackgroundOrderSyncOptions
            {
                Enabled = true,
                InitialDelaySeconds = 0,
                IntervalSeconds = 1,
                BatchSize = 10
            }),
            logger ?? new RecordingLogger<BackgroundOrderSyncScheduler>());
    }

    private sealed class RecordingRunner : IBackgroundOrderSyncRunner
    {
        public List<int> BatchSizes { get; } = [];

        public Task<OrderSyncRunResult> RunAsync(
            int batchSize,
            CancellationToken cancellationToken = default)
        {
            BatchSizes.Add(batchSize);
            return Task.FromResult(new OrderSyncRunResult(1, 1, 0, 0, 0));
        }
    }

    private sealed class BlockingRunner(Task release) : IBackgroundOrderSyncRunner
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int RunCount { get; private set; }

        public async Task<OrderSyncRunResult> RunAsync(
            int batchSize,
            CancellationToken cancellationToken = default)
        {
            RunCount++;
            Started.SetResult();
            await release.WaitAsync(cancellationToken);
            return new OrderSyncRunResult(1, 1, 0, 0, 0);
        }
    }

    private sealed class ThrowingRunner : IBackgroundOrderSyncRunner
    {
        public Task<OrderSyncRunResult> RunAsync(
            int batchSize,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("simulated sync failure");
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception));
        }
    }
}
