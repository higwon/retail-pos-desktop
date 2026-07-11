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

public sealed record ReceiptPrintRequestPayload(
    ReceiptPreview Receipt,
    string PrintableText);

public interface IReceiptPrinterSimulatorControl
{
    event EventHandler? StateChanged;

    ReceiptPrinterConnectionState ConnectionState { get; }
    ReceiptPrinterOperationalState OperationalState { get; }
    DeviceRequest<ReceiptPrintRequestPayload, ReceiptPrintOutcome>? PendingRequest { get; }
    IReadOnlyList<DeviceRequest<ReceiptPrintRequestPayload, ReceiptPrintOutcome>> RecentRequests { get; }

    bool Respond(Guid requestId, ReceiptPrintOutcome outcome);
    void Connect();
    void Disconnect();
    void Reset();
}

public sealed class SimulatedReceiptPrinter : IReceiptPrinter, IReceiptPrinterSimulatorControl, IDisposable
{
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromMinutes(5);
    private readonly object _sync = new();
    private readonly TimeProvider _timeProvider;
    private readonly DeviceRequestQueue<ReceiptPrintRequestPayload, ReceiptPrintOutcome> _requests;
    private ReceiptPrinterConnectionState _connectionState = ReceiptPrinterConnectionState.Connected;
    private ReceiptPrinterOperationalState _operationalState = ReceiptPrinterOperationalState.Ready;
    private bool _disposed;

    public SimulatedReceiptPrinter(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
        _requests = new DeviceRequestQueue<ReceiptPrintRequestPayload, ReceiptPrintOutcome>(
            "Receipt printer",
            timeProvider);
        _requests.Changed += OnRequestsChanged;
    }

    public event EventHandler? StateChanged;

    public ReceiptPrinterConnectionState ConnectionState
    {
        get { lock (_sync) return _connectionState; }
    }

    public ReceiptPrinterOperationalState OperationalState
    {
        get { lock (_sync) return _operationalState; }
    }

    public DeviceRequest<ReceiptPrintRequestPayload, ReceiptPrintOutcome>? PendingRequest =>
        _requests.Pending;

    public IReadOnlyList<DeviceRequest<ReceiptPrintRequestPayload, ReceiptPrintOutcome>> RecentRequests =>
        _requests.Recent;

    public bool Respond(Guid requestId, ReceiptPrintOutcome outcome)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!Enum.IsDefined(outcome) || outcome == ReceiptPrintOutcome.Busy)
        {
            throw new ArgumentOutOfRangeException(
                nameof(outcome),
                outcome,
                "Busy and unknown values are not valid operator responses.");
        }

        var completed = _requests.TryComplete(requestId, outcome);
        if (completed && outcome == ReceiptPrintOutcome.Disconnected)
        {
            lock (_sync)
            {
                _connectionState = ReceiptPrinterConnectionState.Disconnected;
                _operationalState = ReceiptPrinterOperationalState.Disconnected;
            }

            RaiseStateChanged();
        }

        return completed;
    }

    public void Connect()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        lock (_sync)
        {
            _connectionState = ReceiptPrinterConnectionState.Connected;
            _operationalState = _requests.Pending is null
                ? ReceiptPrinterOperationalState.Ready
                : ReceiptPrinterOperationalState.Printing;
        }

        RaiseStateChanged();
    }

    public void Disconnect()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var pending = _requests.Pending;
        lock (_sync)
        {
            _connectionState = ReceiptPrinterConnectionState.Disconnected;
            _operationalState = ReceiptPrinterOperationalState.Disconnected;
        }

        if (pending is not null)
        {
            _requests.TryDisconnect(pending.RequestId);
        }

        RaiseStateChanged();
    }

    public void Reset()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_requests.Pending is not null)
        {
            throw new InvalidOperationException(
                "Receipt printer simulation cannot be reset while printing.");
        }

        lock (_sync)
        {
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

        Task<DeviceRequest<ReceiptPrintRequestPayload, ReceiptPrintOutcome>> completion;
        try
        {
            completion = _requests.BeginAsync(
                receipt.OrderNumber,
                new ReceiptPrintRequestPayload(receipt, receipt.PlainText),
                RequestTimeout,
                cancellationToken);

            var pending = _requests.Pending;
            if (ConnectionState == ReceiptPrinterConnectionState.Disconnected && pending is not null)
            {
                _requests.TryDisconnect(pending.RequestId);
            }
        }
        catch (DeviceRequestBusyException)
        {
            return Result(
                ReceiptPrintOutcome.Busy,
                "Receipt printer is busy. Wait for the current print to finish.");
        }

        var request = await completion;
        var outcome = request.State switch
        {
            DeviceRequestState.Completed => request.Result,
            DeviceRequestState.Cancelled or DeviceRequestState.Disposed => ReceiptPrintOutcome.Cancelled,
            DeviceRequestState.TimedOut => ReceiptPrintOutcome.Timeout,
            DeviceRequestState.Disconnected => ReceiptPrintOutcome.Disconnected,
            _ => ReceiptPrintOutcome.Failed
        };

        return Complete(outcome);
    }

    private ReceiptPrintResult Complete(ReceiptPrintOutcome outcome)
    {
        lock (_sync)
        {
            if (outcome == ReceiptPrintOutcome.Disconnected)
            {
                _connectionState = ReceiptPrinterConnectionState.Disconnected;
                _operationalState = ReceiptPrinterOperationalState.Disconnected;
            }
            else if (_connectionState == ReceiptPrinterConnectionState.Connected)
            {
                _operationalState = outcome is ReceiptPrintOutcome.Printed or ReceiptPrintOutcome.Cancelled
                    ? ReceiptPrinterOperationalState.Ready
                    : ReceiptPrinterOperationalState.Faulted;
            }
        }

        RaiseStateChanged();
        return outcome switch
        {
            ReceiptPrintOutcome.Printed => new ReceiptPrintResult(
                outcome,
                _timeProvider.GetUtcNow(),
                "Receipt printed successfully."),
            ReceiptPrintOutcome.PaperOut => Result(outcome, "Receipt printer is out of paper. Refill paper and retry."),
            ReceiptPrintOutcome.CoverOpen => Result(outcome, "Receipt printer cover is open. Close it and retry."),
            ReceiptPrintOutcome.Disconnected => Result(outcome, "Receipt printer is disconnected. Connect it and try again."),
            ReceiptPrintOutcome.Timeout => Result(outcome, "Receipt printer did not respond in time. Check it and retry."),
            ReceiptPrintOutcome.Cancelled => Result(outcome, "Receipt printing was cancelled. The receipt can be retried."),
            ReceiptPrintOutcome.Busy => Result(outcome, "Receipt printer is busy. Wait for the current print to finish."),
            ReceiptPrintOutcome.Failed => Result(outcome, "Receipt could not be printed. Check the printer and retry."),
            _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, null)
        };
    }

    private void OnRequestsChanged(object? sender, EventArgs e)
    {
        lock (_sync)
        {
            if (_connectionState == ReceiptPrinterConnectionState.Connected && _requests.Pending is not null)
            {
                _operationalState = ReceiptPrinterOperationalState.Printing;
            }
        }

        RaiseStateChanged();
    }

    private static ReceiptPrintResult Result(ReceiptPrintOutcome outcome, string message) =>
        new(outcome, null, message);

    private void RaiseStateChanged() => StateChanged?.Invoke(this, EventArgs.Empty);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _requests.Changed -= OnRequestsChanged;
        _requests.Dispose();
    }
}
