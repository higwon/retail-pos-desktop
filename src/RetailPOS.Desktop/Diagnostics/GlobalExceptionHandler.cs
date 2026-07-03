using Microsoft.Extensions.Logging;
using System.Windows;
using System.Windows.Threading;

namespace RetailPOS.Desktop.Diagnostics;

public sealed class GlobalExceptionHandler
{
    private readonly Application _application;
    private readonly ILogger<GlobalExceptionHandler> _logger;
    private bool _isRegistered;

    public GlobalExceptionHandler(Application application, ILogger<GlobalExceptionHandler> logger)
    {
        _application = application;
        _logger = logger;
    }

    public void Register()
    {
        if (_isRegistered)
        {
            return;
        }

        _application.DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        _isRegistered = true;
    }

    public void Unregister()
    {
        if (!_isRegistered)
        {
            return;
        }

        _application.DispatcherUnhandledException -= OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException -= OnAppDomainUnhandledException;
        TaskScheduler.UnobservedTaskException -= OnUnobservedTaskException;
        _isRegistered = false;
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        _logger.LogCritical(e.Exception, "An unhandled UI exception terminated the application.");
        MessageBox.Show(
            "Retail POS encountered an unexpected error and must close.",
            "Unexpected error",
            MessageBoxButton.OK,
            MessageBoxImage.Error);

        e.Handled = true;
        _application.Shutdown(-1);
    }

    private void OnAppDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception exception)
        {
            _logger.LogCritical(exception, "An unhandled application exception occurred. Terminating: {IsTerminating}", e.IsTerminating);
            return;
        }

        _logger.LogCritical("An unknown unhandled application error occurred. Terminating: {IsTerminating}", e.IsTerminating);
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        _logger.LogError(e.Exception, "An unobserved background task exception occurred.");
        e.SetObserved();
    }
}
