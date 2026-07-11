using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RetailPOS.Desktop.DependencyInjection;
using RetailPOS.Desktop.Configuration;
using RetailPOS.Desktop.DeviceSimulation;
using RetailPOS.Desktop.Diagnostics;
using RetailPOS.Desktop.Sync;
using RetailPOS.Infrastructure.DependencyInjection;
using RetailPOS.Infrastructure.Persistence;
using Serilog;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace RetailPOS.Desktop;

public partial class App : System.Windows.Application
{
    private IHost? _host;
    private AsyncServiceScope? _uiScope;
    private GlobalExceptionHandler? _exceptionHandler;

    protected override async void OnStartup(StartupEventArgs e)
    {
        try
        {
            base.OnStartup(e);

            var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
            {
                Args = e.Args,
                ContentRootPath = AppContext.BaseDirectory
            });
            builder.Configuration.AddEnvironmentVariables("RETAILPOS_");
            var bootstrapLogger = CreateBootstrapLogger();
            Log.Logger = bootstrapLogger;
            var startupConfiguration = DesktopStartupConfiguration.Create(
                builder.Configuration,
                CreateLogger);
            Log.Logger = startupConfiguration.Logger;
            (bootstrapLogger as IDisposable)?.Dispose();
            var validatedConfiguration = startupConfiguration.Values;
            builder.Logging.ClearProviders();
            builder.Services.AddSerilog(Log.Logger, dispose: true);
            builder.Services.AddSingleton<System.Windows.Application>(this);
            builder.Services.AddLocalPersistence(builder.Configuration);
            builder.Services.AddApiSyncClient(validatedConfiguration.ApiBaseAddress);
            builder.Services.AddSingleton(TimeProvider.System);
            builder.Services.Configure<BackgroundOrderSyncOptions>(
                builder.Configuration.GetSection(BackgroundOrderSyncOptions.SectionName));
            builder.Services.Configure<ApiConnectivityMonitorOptions>(
                builder.Configuration.GetSection(ApiConnectivityMonitorOptions.SectionName));
            builder.Services.Configure<DeviceSimulationOptions>(
                builder.Configuration.GetSection(DeviceSimulationOptions.SectionName));
            builder.Services.AddSingleton<IApiConnectivityStateStore, ApiConnectivityStateStore>();
            builder.Services.AddHttpClient<IApiConnectivityClient, HttpApiConnectivityClient>(client =>
            {
                client.BaseAddress = validatedConfiguration.ApiBaseAddress;
                client.Timeout = TimeSpan.FromSeconds(5);
            });
            builder.Services.AddScoped<IBackgroundOrderSyncRunner, BackgroundOrderSyncRunner>();
            builder.Services.AddSingleton<BackgroundOrderSyncScheduler>();
            builder.Services.AddHostedService(provider => provider.GetRequiredService<BackgroundOrderSyncScheduler>());
            builder.Services.AddHostedService<ApiConnectivityMonitor>();
            builder.Services.AddDesktopServices(validatedConfiguration.EnableDemoLogin);

            _host = builder.Build();
            _exceptionHandler = _host.Services.GetRequiredService<GlobalExceptionHandler>();
            _exceptionHandler.Register();

            var logger = _host.Services.GetRequiredService<ILogger<App>>();
            logger.LogInformation(
                "Retail POS starting with profile {Profile}. Demo login enabled: {DemoLoginEnabled}.",
                validatedConfiguration.Profile,
                validatedConfiguration.EnableDemoLogin);
            await using (var scope = _host.Services.CreateAsyncScope())
            {
                await scope.ServiceProvider
                    .GetRequiredService<LocalDatabaseInitializer>()
                    .InitializeAsync();
            }
            logger.LogInformation("Retail POS local database is ready.");

            await _host.StartAsync();

            _uiScope = _host.Services.CreateAsyncScope();
            var mainWindow = _uiScope.Value.ServiceProvider.GetRequiredService<MainWindow>();
            MainWindow = mainWindow;
            mainWindow.Show();
        }
        catch (Exception exception)
        {
            Log.Fatal(exception, "Retail POS failed during startup.");
            _host?.Services.GetService<ILogger<App>>()?
                .LogCritical(exception, "Retail POS failed during startup.");
            Trace.TraceError("Retail POS failed to start: {0}", exception);
            MessageBox.Show(
                "Retail POS could not start. Close the application and try again.",
                "Startup error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            Shutdown(-1);
        }
    }

    private static Serilog.ILogger CreateBootstrapLogger() =>
        new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Debug()
            .CreateBootstrapLogger();

    private static Serilog.ILogger CreateLogger(IConfiguration configuration)
    {
        var logDirectory = configuration["Serilog:Desktop:LogDirectory"];
        if (string.IsNullOrWhiteSpace(logDirectory))
        {
            logDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "RetailPOS",
                "logs");
        }

        var retainedFileCountLimit = configuration.GetValue("Serilog:Desktop:RetainedFileCountLimit", 14);
        var minimumLevel = configuration.GetValue("Serilog:MinimumLevel:Default", "Information");
        var logPath = Path.Combine(logDirectory, "retail-pos-.log");

        return new LoggerConfiguration()
            .MinimumLevel.Is(ParseMinimumLevel(minimumLevel))
            .Enrich.FromLogContext()
            .WriteTo.Debug()
            .WriteTo.File(
                logPath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: retainedFileCountLimit,
                outputTemplate:
                "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext} {Message:lj} {Properties:j}{NewLine}{Exception}")
            .CreateLogger();
    }

    private static Serilog.Events.LogEventLevel ParseMinimumLevel(string? value) =>
        Enum.TryParse<Serilog.Events.LogEventLevel>(value, ignoreCase: true, out var level)
            ? level
            : Serilog.Events.LogEventLevel.Information;

    protected override void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            _host.Services.GetService<ILogger<App>>()?.LogInformation("Retail POS exiting with code {ExitCode}.", e.ApplicationExitCode);
            try
            {
                _host.StopAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
            }
            catch (Exception exception)
            {
                _host.Services.GetService<ILogger<App>>()?
                    .LogWarning(exception, "Retail POS host did not stop cleanly.");
            }
        }

        _exceptionHandler?.Unregister();
        if (_uiScope is { } uiScope)
        {
            uiScope.Dispose();
        }
        _host?.Dispose();
        base.OnExit(e);
    }
}
