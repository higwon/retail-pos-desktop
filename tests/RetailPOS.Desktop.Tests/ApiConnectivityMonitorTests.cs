using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RetailPOS.Application.Sync;
using RetailPOS.Desktop.Sync;

namespace RetailPOS.Desktop.Tests;

public sealed class ApiConnectivityMonitorTests
{
    [Fact]
    public async Task HttpApiConnectivityClient_CheckAsync_UsesApiHealthEndpoint()
    {
        var handler = new RecordingHttpMessageHandler();
        var client = new HttpApiConnectivityClient(
            new HttpClient(handler)
            {
                BaseAddress = new Uri("http://localhost:5000/")
            },
            TimeProvider.System);

        var snapshot = await client.CheckAsync();

        Assert.Equal(ApiConnectivityStatus.Online, snapshot.Status);
        Assert.Equal(new Uri("http://localhost:5000/api/health"), handler.RequestUri);
    }

    [Fact]
    public async Task CheckOnceAsync_WhenApiRecovers_UpdatesStateAndTriggersOneSyncRun()
    {
        var stateStore = StateStore(ApiConnectivityStatus.Offline);
        var runner = new RecordingRunner();
        var monitor = Monitor(
            new SequenceConnectivityClient(
                new ApiConnectivitySnapshot(ApiConnectivityStatus.Online, DateTimeOffset.UtcNow, null)),
            stateStore,
            Scheduler(runner, stateStore));

        var snapshot = await monitor.CheckOnceAsync();

        Assert.Equal(ApiConnectivityStatus.Online, snapshot.Status);
        Assert.Equal(ApiConnectivityStatus.Online, stateStore.Current.Status);
        Assert.Equal([10], runner.BatchSizes);
    }

    [Fact]
    public async Task CheckOnceAsync_WhenApiStaysOffline_DoesNotRunSync()
    {
        var stateStore = StateStore(ApiConnectivityStatus.Offline);
        var runner = new RecordingRunner();
        var monitor = Monitor(
            new SequenceConnectivityClient(
                new ApiConnectivitySnapshot(ApiConnectivityStatus.Offline, DateTimeOffset.UtcNow, "HttpRequestException")),
            stateStore,
            Scheduler(runner, stateStore));

        var snapshot = await monitor.CheckOnceAsync();

        Assert.Equal(ApiConnectivityStatus.Offline, snapshot.Status);
        Assert.Empty(runner.BatchSizes);
    }

    [Fact]
    public async Task CheckOnceAsync_WhenInitialUnknownBecomesOnline_DoesNotDuplicateSchedulerInitialRun()
    {
        var stateStore = StateStore(ApiConnectivityStatus.Unknown);
        var runner = new RecordingRunner();
        var monitor = Monitor(
            new SequenceConnectivityClient(
                new ApiConnectivitySnapshot(ApiConnectivityStatus.Online, DateTimeOffset.UtcNow, null)),
            stateStore,
            Scheduler(runner, stateStore));

        var snapshot = await monitor.CheckOnceAsync();

        Assert.Equal(ApiConnectivityStatus.Online, snapshot.Status);
        Assert.Empty(runner.BatchSizes);
    }

    [Fact]
    public async Task CheckOnceAsync_WhenSchedulerIsDisabled_DoesNotTriggerSync()
    {
        var stateStore = StateStore(ApiConnectivityStatus.Offline);
        var runner = new RecordingRunner();
        var monitor = Monitor(
            new SequenceConnectivityClient(
                new ApiConnectivitySnapshot(ApiConnectivityStatus.Online, DateTimeOffset.UtcNow, null)),
            stateStore,
            Scheduler(runner, stateStore),
            syncEnabled: false);

        await monitor.CheckOnceAsync();

        Assert.Empty(runner.BatchSizes);
    }

    private static ApiConnectivityMonitor Monitor(
        IApiConnectivityClient client,
        IApiConnectivityStateStore stateStore,
        BackgroundOrderSyncScheduler scheduler,
        bool syncEnabled = true) =>
        new(
            client,
            stateStore,
            scheduler,
            Options.Create(new ApiConnectivityMonitorOptions
            {
                Enabled = true,
                InitialDelaySeconds = 0,
                IntervalSeconds = 30,
                TriggerSyncOnReconnect = true
            }),
            Options.Create(new BackgroundOrderSyncOptions
            {
                Enabled = syncEnabled,
                InitialDelaySeconds = 0,
                IntervalSeconds = 1,
                BatchSize = 10
            }),
            new RecordingLogger<ApiConnectivityMonitor>());

    private static BackgroundOrderSyncScheduler Scheduler(
        IBackgroundOrderSyncRunner runner,
        IApiConnectivityStateStore stateStore)
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => runner);
        return new BackgroundOrderSyncScheduler(
            services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(),
            stateStore,
            Options.Create(new BackgroundOrderSyncOptions
            {
                Enabled = true,
                InitialDelaySeconds = 0,
                IntervalSeconds = 1,
                BatchSize = 10
            }),
            new RecordingLogger<BackgroundOrderSyncScheduler>());
    }

    private static ApiConnectivityStateStore StateStore(ApiConnectivityStatus status)
    {
        var store = new ApiConnectivityStateStore();
        store.Update(new ApiConnectivitySnapshot(status, DateTimeOffset.UtcNow, null));
        return store;
    }

    private sealed class SequenceConnectivityClient(params ApiConnectivitySnapshot[] snapshots) : IApiConnectivityClient
    {
        private readonly Queue<ApiConnectivitySnapshot> _snapshots = new(snapshots);

        public Task<ApiConnectivitySnapshot> CheckAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(_snapshots.Dequeue());
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

    private sealed class RecordingHttpMessageHandler : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK));
        }
    }
}
