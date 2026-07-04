using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RetailPOS.Desktop.DependencyInjection;
using RetailPOS.Desktop.Diagnostics;
using RetailPOS.Infrastructure.DependencyInjection;
using RetailPOS.Infrastructure.Persistence;
using System.Diagnostics;
using System.Windows;

namespace RetailPOS.Desktop;

public partial class App : System.Windows.Application
{
    private IHost? _host;
    private GlobalExceptionHandler? _exceptionHandler;

    protected override void OnStartup(StartupEventArgs e)
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
            builder.Logging.ClearProviders();
            builder.Logging.AddConfiguration(builder.Configuration.GetSection("Logging"));
            builder.Logging.AddDebug();
            builder.Services.AddSingleton<System.Windows.Application>(this);
            builder.Services.AddLocalPersistence(builder.Configuration);
            builder.Services.AddDesktopServices();

            _host = builder.Build();
            _exceptionHandler = _host.Services.GetRequiredService<GlobalExceptionHandler>();
            _exceptionHandler.Register();

            var logger = _host.Services.GetRequiredService<ILogger<App>>();
            _host.Services.GetRequiredService<LocalDatabaseInitializer>()
                .InitializeAsync()
                .GetAwaiter()
                .GetResult();
            logger.LogInformation("Retail POS local database is ready.");

            var mainWindow = _host.Services.GetRequiredService<MainWindow>();
            MainWindow = mainWindow;
            mainWindow.Show();
        }
        catch (Exception exception)
        {
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

    protected override void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            _host.Services.GetService<ILogger<App>>()?.LogInformation("Retail POS exiting with code {ExitCode}.", e.ApplicationExitCode);
        }

        _exceptionHandler?.Unregister();
        _host?.Dispose();
        base.OnExit(e);
    }
}
