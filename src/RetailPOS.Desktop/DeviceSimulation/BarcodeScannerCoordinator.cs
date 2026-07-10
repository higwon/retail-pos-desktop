using Microsoft.Extensions.Logging;
using RetailPOS.Application.Devices;
using RetailPOS.Desktop.ViewModels;
using System.Windows.Threading;

namespace RetailPOS.Desktop.DeviceSimulation;

public interface IUiDispatcher
{
    bool CheckAccess();
    Task InvokeAsync(Func<Task> action);
}

public sealed class WpfUiDispatcher(Dispatcher dispatcher) : IUiDispatcher
{
    public bool CheckAccess() => dispatcher.CheckAccess();

    public Task InvokeAsync(Func<Task> action) => CheckAccess()
        ? action()
        : dispatcher.InvokeAsync(action).Task.Unwrap();
}

public sealed class BarcodeScannerCoordinator(
    IBarcodeScanner scanner,
    ProductGridViewModel productGrid,
    IUiDispatcher dispatcher,
    ILogger<BarcodeScannerCoordinator> logger) : IDisposable
{
    private readonly SemaphoreSlim _scanGate = new(1, 1);
    private readonly CancellationTokenSource _lifetimeCancellation = new();
    private readonly object _sessionSync = new();
    private CancellationTokenSource? _sessionCancellation;
    private bool _started;
    private bool _disposed;

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        lock (_sessionSync)
        {
            if (_started)
            {
                return;
            }

            _sessionCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                _lifetimeCancellation.Token);
            scanner.BarcodeScanned += OnBarcodeScanned;
            _started = true;
        }
    }

    public void Stop()
    {
        CancellationTokenSource? sessionCancellation;
        lock (_sessionSync)
        {
            if (!_started)
            {
                return;
            }

            scanner.BarcodeScanned -= OnBarcodeScanned;
            _started = false;
            sessionCancellation = _sessionCancellation;
            _sessionCancellation = null;
        }

        sessionCancellation?.Cancel();
        sessionCancellation?.Dispose();
    }

    private async void OnBarcodeScanned(object? sender, BarcodeScannedEventArgs e)
    {
        CancellationToken sessionToken;
        lock (_sessionSync)
        {
            if (!_started || _sessionCancellation is null)
            {
                return;
            }

            sessionToken = _sessionCancellation.Token;
        }

        try
        {
            await _scanGate.WaitAsync(sessionToken);
            try
            {
                await dispatcher.InvokeAsync(() =>
                {
                    sessionToken.ThrowIfCancellationRequested();
                    return productGrid.ProcessBarcodeAsync(e.Barcode, sessionToken);
                });
            }
            finally
            {
                _scanGate.Release();
            }
        }
        catch (OperationCanceledException) when (sessionToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Barcode scanner event could not be processed.");
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Stop();
        _lifetimeCancellation.Cancel();
        _lifetimeCancellation.Dispose();
    }
}
