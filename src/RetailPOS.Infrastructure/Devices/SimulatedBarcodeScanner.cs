using RetailPOS.Application.Devices;

namespace RetailPOS.Infrastructure.Devices;

public enum BarcodeScannerConnectionState
{
    Disconnected,
    Connected
}

public interface IBarcodeScannerSimulatorControl
{
    event EventHandler? StateChanged;

    BarcodeScannerConnectionState ConnectionState { get; }
    bool EmitOnBackgroundThread { get; set; }

    void Connect();
    void Disconnect();
    Task<bool> EmitAsync(string barcode, CancellationToken cancellationToken = default);
}

public sealed class SimulatedBarcodeScanner :
    IBarcodeScanner,
    IBarcodeScannerSimulatorControl,
    IDisposable
{
    private readonly object _sync = new();
    private BarcodeScannerConnectionState _connectionState = BarcodeScannerConnectionState.Connected;
    private bool _emitOnBackgroundThread;
    private bool _disposed;

    public event EventHandler<BarcodeScannedEventArgs>? BarcodeScanned;
    public event EventHandler? StateChanged;

    public BarcodeScannerConnectionState ConnectionState
    {
        get
        {
            lock (_sync)
            {
                return _connectionState;
            }
        }
    }

    public bool EmitOnBackgroundThread
    {
        get
        {
            lock (_sync)
            {
                return _emitOnBackgroundThread;
            }
        }
        set
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            lock (_sync)
            {
                _emitOnBackgroundThread = value;
            }

            StateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Connect()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        lock (_sync)
        {
            _connectionState = BarcodeScannerConnectionState.Connected;
        }

        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Disconnect()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        lock (_sync)
        {
            _connectionState = BarcodeScannerConnectionState.Disconnected;
        }

        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task<bool> EmitAsync(
        string barcode,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (string.IsNullOrWhiteSpace(barcode))
        {
            throw new ArgumentException("A barcode is required.", nameof(barcode));
        }

        bool emitOnBackgroundThread;
        lock (_sync)
        {
            if (_connectionState != BarcodeScannerConnectionState.Connected)
            {
                return false;
            }

            emitOnBackgroundThread = _emitOnBackgroundThread;
        }

        var args = new BarcodeScannedEventArgs(barcode);
        if (emitOnBackgroundThread)
        {
            await Task.Run(
                () => BarcodeScanned?.Invoke(this, args),
                cancellationToken);
        }
        else
        {
            cancellationToken.ThrowIfCancellationRequested();
            BarcodeScanned?.Invoke(this, args);
        }

        return true;
    }

    public void Dispose()
    {
        _disposed = true;
        BarcodeScanned = null;
        StateChanged = null;
    }
}
