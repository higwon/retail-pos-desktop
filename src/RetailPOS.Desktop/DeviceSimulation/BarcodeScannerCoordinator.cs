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
    private bool _started;
    private bool _disposed;

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_started)
        {
            return;
        }

        scanner.BarcodeScanned += OnBarcodeScanned;
        _started = true;
    }

    public void Stop()
    {
        if (!_started)
        {
            return;
        }

        scanner.BarcodeScanned -= OnBarcodeScanned;
        _started = false;
    }

    private async void OnBarcodeScanned(object? sender, BarcodeScannedEventArgs e)
    {
        try
        {
            await _scanGate.WaitAsync(_lifetimeCancellation.Token);
            try
            {
                await dispatcher.InvokeAsync(() =>
                    productGrid.ProcessBarcodeAsync(e.Barcode, _lifetimeCancellation.Token));
            }
            finally
            {
                _scanGate.Release();
            }
        }
        catch (OperationCanceledException) when (_lifetimeCancellation.IsCancellationRequested)
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
