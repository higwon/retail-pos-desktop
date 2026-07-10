using RetailPOS.Application.Receipts;

namespace RetailPOS.Infrastructure.Devices;

public enum ReceiptPrinterConnectionState
{
    Disconnected,
    Connected
}

public enum ReceiptPrinterOperationalState
{
    Disconnected,
    Ready,
    Printing,
    Faulted
}

public sealed record ReceiptPrinterSimulationSettings(
    ReceiptPrintOutcome NextOutcome,
    TimeSpan ResponseDelay)
{
    public static ReceiptPrinterSimulationSettings Default { get; } =
        new(ReceiptPrintOutcome.Printed, TimeSpan.Zero);
}

public interface IReceiptPrinterSimulatorControl
{
    event EventHandler? StateChanged;

    ReceiptPrinterSimulationSettings CurrentSettings { get; }
    ReceiptPrinterConnectionState ConnectionState { get; }
    ReceiptPrinterOperationalState OperationalState { get; }

    void ConfigureNext(ReceiptPrinterSimulationSettings settings);
    void Connect();
    void Disconnect();
    void Reset();
}

public sealed class SimulatedReceiptPrinter(TimeProvider timeProvider)
    : IReceiptPrinter, IReceiptPrinterSimulatorControl, IDisposable
{
    private readonly object _sync = new();
    private readonly SemaphoreSlim _printGate = new(1, 1);
    private ReceiptPrinterSimulationSettings _settings = ReceiptPrinterSimulationSettings.Default;
    private ReceiptPrinterConnectionState _connectionState = ReceiptPrinterConnectionState.Connected;
    private ReceiptPrinterOperationalState _operationalState = ReceiptPrinterOperationalState.Ready;
    private CancellationTokenSource? _activePrintCancellation;
    private bool _disposed;

    public event EventHandler? StateChanged;

    public ReceiptPrinterSimulationSettings CurrentSettings
    {
        get
        {
            lock (_sync)
            {
                return _settings;
            }
        }
    }

    public ReceiptPrinterConnectionState ConnectionState
    {
        get
        {
            lock (_sync)
            {
                return _connectionState;
            }
        }
    }

    public ReceiptPrinterOperationalState OperationalState
    {
        get
        {
            lock (_sync)
            {
                return _operationalState;
            }
        }
    }

    public void ConfigureNext(ReceiptPrinterSimulationSettings settings)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(settings);
        if (settings.ResponseDelay < TimeSpan.Zero || settings.ResponseDelay > TimeSpan.FromMinutes(1))
        {
            throw new ArgumentOutOfRangeException(
                nameof(settings),
                "Receipt printer response delay must be between zero and one minute.");
        }

        if (!Enum.IsDefined(settings.NextOutcome))
        {
            throw new ArgumentOutOfRangeException(
                nameof(settings),
                settings.NextOutcome,
                "Unsupported receipt printer simulation outcome.");
        }

        lock (_sync)
        {
            _settings = settings;
        }

        RaiseStateChanged();
    }

    public void Connect()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        lock (_sync)
        {
            if (_connectionState == ReceiptPrinterConnectionState.Connected)
            {
                return;
            }

            _connectionState = ReceiptPrinterConnectionState.Connected;
            _operationalState = ReceiptPrinterOperationalState.Ready;
        }

        RaiseStateChanged();
    }

    public void Disconnect()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        CancellationTokenSource? active;
        lock (_sync)
        {
            if (_connectionState == ReceiptPrinterConnectionState.Disconnected)
            {
                return;
            }

            _connectionState = ReceiptPrinterConnectionState.Disconnected;
            _operationalState = ReceiptPrinterOperationalState.Disconnected;
            active = _activePrintCancellation;
        }

        active?.Cancel();
        RaiseStateChanged();
    }

    public void Reset()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        lock (_sync)
        {
            _settings = ReceiptPrinterSimulationSettings.Default;
            _operationalState = _connectionState == ReceiptPrinterConnectionState.Connected
                ? ReceiptPrinterOperationalState.Ready
                : ReceiptPrinterOperationalState.Disconnected;
        }

        RaiseStateChanged();
    }

    public async Task<ReceiptPrintResult> PrintAsync(
        ReceiptPreview receipt,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(receipt);
        cancellationToken.ThrowIfCancellationRequested();

        if (ConnectionState == ReceiptPrinterConnectionState.Disconnected)
        {
            return Result(
                ReceiptPrintOutcome.Disconnected,
                "Receipt printer is disconnected. Connect it and try again.");
        }

        if (!await _printGate.WaitAsync(0, cancellationToken))
        {
            return Result(
                ReceiptPrintOutcome.Busy,
                "Receipt printer is busy. Wait for the current print to finish.");
        }

        CancellationTokenSource? linkedCancellation = null;
        try
        {
            var settings = TakeSettingsAndStartPrint();
            linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            lock (_sync)
            {
                _activePrintCancellation = linkedCancellation;
            }

            RaiseStateChanged();
            if (settings.ResponseDelay > TimeSpan.Zero)
            {
                await Task.Delay(settings.ResponseDelay, timeProvider, linkedCancellation.Token);
            }

            linkedCancellation.Token.ThrowIfCancellationRequested();
            return Complete(settings.NextOutcome);
        }
        catch (OperationCanceledException)
        {
            if (ConnectionState == ReceiptPrinterConnectionState.Disconnected)
            {
                return Result(
                    ReceiptPrintOutcome.Disconnected,
                    "Receipt printer disconnected before printing completed.");
            }

            SetOperationalState(ReceiptPrinterOperationalState.Ready);
            return Result(
                ReceiptPrintOutcome.Cancelled,
                "Receipt printing was cancelled. The receipt can be retried.");
        }
        finally
        {
            lock (_sync)
            {
                if (ReferenceEquals(_activePrintCancellation, linkedCancellation))
                {
                    _activePrintCancellation = null;
                }
            }

            linkedCancellation?.Dispose();
            _printGate.Release();
            RaiseStateChanged();
        }
    }

    private ReceiptPrinterSimulationSettings TakeSettingsAndStartPrint()
    {
        lock (_sync)
        {
            var settings = _settings;
            _settings = ReceiptPrinterSimulationSettings.Default;
            _operationalState = ReceiptPrinterOperationalState.Printing;
            return settings;
        }
    }

    private ReceiptPrintResult Complete(ReceiptPrintOutcome outcome)
    {
        var result = outcome switch
        {
            ReceiptPrintOutcome.Printed => new ReceiptPrintResult(
                outcome,
                timeProvider.GetUtcNow(),
                "Receipt printed successfully."),
            ReceiptPrintOutcome.PaperOut => Result(
                outcome,
                "Receipt printer is out of paper. Refill paper and retry."),
            ReceiptPrintOutcome.CoverOpen => Result(
                outcome,
                "Receipt printer cover is open. Close it and retry."),
            ReceiptPrintOutcome.Disconnected => Result(
                outcome,
                "Receipt printer is disconnected. Connect it and try again."),
            ReceiptPrintOutcome.Timeout => Result(
                outcome,
                "Receipt printer did not respond in time. Check it and retry."),
            ReceiptPrintOutcome.Cancelled => Result(
                outcome,
                "Receipt printing was cancelled. The receipt can be retried."),
            ReceiptPrintOutcome.Busy => Result(
                outcome,
                "Receipt printer is busy. Wait for the current print to finish."),
            ReceiptPrintOutcome.Failed => Result(
                outcome,
                "Receipt could not be printed. Check the printer and retry."),
            _ => throw new ArgumentOutOfRangeException(
                nameof(outcome),
                outcome,
                "Unsupported receipt printer outcome.")
        };

        lock (_sync)
        {
            if (outcome == ReceiptPrintOutcome.Disconnected)
            {
                _connectionState = ReceiptPrinterConnectionState.Disconnected;
                _operationalState = ReceiptPrinterOperationalState.Disconnected;
            }
            else
            {
                _operationalState = outcome is
                    ReceiptPrintOutcome.Printed or
                    ReceiptPrintOutcome.Cancelled or
                    ReceiptPrintOutcome.Busy
                    ? ReceiptPrinterOperationalState.Ready
                    : ReceiptPrinterOperationalState.Faulted;
            }
        }

        return result;
    }

    private void SetOperationalState(ReceiptPrinterOperationalState state)
    {
        lock (_sync)
        {
            _operationalState = state;
        }
    }

    private static ReceiptPrintResult Result(ReceiptPrintOutcome outcome, string message) =>
        new(outcome, null, message);

    private void RaiseStateChanged() => StateChanged?.Invoke(this, EventArgs.Empty);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        lock (_sync)
        {
            _activePrintCancellation?.Cancel();
        }
    }
}
