using Microsoft.Extensions.Options;
using RetailPOS.Application.Devices;
using RetailPOS.Infrastructure.Devices;

namespace RetailPOS.Desktop.DeviceSimulation;

public sealed class DeviceStatusService : IDisposable
{
    private readonly object _sync = new();
    private readonly object _refreshGate = new();
    private readonly IBarcodeScannerSimulatorControl _scanner;
    private readonly IReceiptPrinterSimulatorControl _printer;
    private readonly IPaymentTerminalSimulatorControl _terminal;
    private readonly CustomerDisplayHost _customerDisplay;
    private readonly IDisplayTargetProvider _displayTargetProvider;
    private readonly IDisplayTopologyMonitor _displayTopologyMonitor;
    private readonly DeviceSimulationOptions _options;
    private readonly TimeProvider _timeProvider;
    private DeviceStatusOverview _current;
    private bool _disposed;

    public DeviceStatusService(
        IBarcodeScannerSimulatorControl scanner,
        IReceiptPrinterSimulatorControl printer,
        IPaymentTerminalSimulatorControl terminal,
        CustomerDisplayHost customerDisplay,
        IDisplayTargetProvider displayTargetProvider,
        IDisplayTopologyMonitor displayTopologyMonitor,
        IOptions<DeviceSimulationOptions> options,
        TimeProvider timeProvider)
    {
        _scanner = scanner;
        _printer = printer;
        _terminal = terminal;
        _customerDisplay = customerDisplay;
        _displayTargetProvider = displayTargetProvider;
        _displayTopologyMonitor = displayTopologyMonitor;
        _options = options.Value;
        _timeProvider = timeProvider;
        var now = UtcNow();
        _current = new DeviceStatusOverview([
            Unknown("scanner", "Barcode scanner", now),
            Unknown("printer", "Receipt printer", now),
            Unknown("terminal", "Card terminal", now),
            Unknown("display", "Customer display", now)]);
        _scanner.StateChanged += OnChanged;
        _printer.StateChanged += OnChanged;
        _terminal.StateChanged += OnChanged;
        _customerDisplay.StateChanged += OnChanged;
        _displayTopologyMonitor.Changed += OnChanged;
    }

    public event EventHandler? Changed;
    public DeviceStatusOverview Current { get { lock (_sync) return _current; } }

    public void Refresh()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        DeviceStatusOverview? updated = null;
        lock (_refreshGate)
        {
            var now = UtcNow();
            lock (_sync)
            {
                var previous = _current.Devices.ToDictionary(x => x.DeviceId);
                updated = new DeviceStatusOverview(_options.Enabled
                    ? [
                        PreserveTime(Scanner(now), previous),
                        PreserveTime(Printer(now), previous),
                        PreserveTime(Terminal(now), previous),
                        PreserveTime(Display(now), previous)]
                    : [
                        PreserveTime(Disabled("scanner", "Barcode scanner", now), previous),
                        PreserveTime(Disabled("printer", "Receipt printer", now), previous),
                        PreserveTime(Disabled("terminal", "Card terminal", now), previous),
                        PreserveTime(Disabled("display", "Customer display", now), previous)]);
                if (_current.Devices.SequenceEqual(updated.Devices)) return;
                _current = updated;
            }
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void OnChanged(object? sender, EventArgs e)
    {
        Refresh();
    }

    private DeviceStatusSnapshot Scanner(DateTimeOffset now) =>
        _scanner.ConnectionState == BarcodeScannerConnectionState.Connected
            ? new("scanner", "Barcode scanner", DeviceAvailability.Available, DeviceReadiness.Ready, "Ready to scan.", now)
            : new("scanner", "Barcode scanner", DeviceAvailability.Unavailable, DeviceReadiness.Unknown, "Scanner disconnected.", now);

    private DeviceStatusSnapshot Printer(DateTimeOffset now) => _printer.ConnectionState == ReceiptPrinterConnectionState.Disconnected
        ? new("printer", "Receipt printer", DeviceAvailability.Unavailable, DeviceReadiness.Unknown, "Printer disconnected.", now)
        : _printer.OperationalState switch
        {
            ReceiptPrinterOperationalState.Printing => new("printer", "Receipt printer", DeviceAvailability.Available, DeviceReadiness.Busy, "Printing receipt.", now),
            ReceiptPrinterOperationalState.Faulted => new("printer", "Receipt printer", DeviceAvailability.Available, DeviceReadiness.Attention, "Printer needs attention.", now),
            _ => new("printer", "Receipt printer", DeviceAvailability.Available, DeviceReadiness.Ready, "Ready to print.", now)
        };

    private DeviceStatusSnapshot Terminal(DateTimeOffset now) => _terminal.ConnectionState == PaymentTerminalConnectionState.Disconnected
        ? new("terminal", "Card terminal", DeviceAvailability.Unavailable, DeviceReadiness.Unknown, "Terminal disconnected.", now)
        : _terminal.OperationalState switch
        {
            PaymentTerminalOperationalState.Processing => new("terminal", "Card terminal", DeviceAvailability.Available, DeviceReadiness.Busy, "Authorization in progress.", now),
            PaymentTerminalOperationalState.Faulted or PaymentTerminalOperationalState.Unknown => new("terminal", "Card terminal", DeviceAvailability.Available, DeviceReadiness.Attention, "Payment outcome needs review.", now),
            _ => new("terminal", "Card terminal", DeviceAvailability.Available, DeviceReadiness.Ready, "Ready for payment.", now)
        };

    private DeviceStatusSnapshot Display(DateTimeOffset now)
    {
        var hasSecondary = _displayTargetProvider.GetTargets().Any(target => !target.IsPrimary);
        if (!hasSecondary)
            return new("display", "Customer display", DeviceAvailability.Unavailable, DeviceReadiness.Unknown, "No secondary monitor available.", now);
        return _customerDisplay.IsOpen
            ? new("display", "Customer display", DeviceAvailability.Available, DeviceReadiness.Ready, "Customer display open.", now)
            : new("display", "Customer display", DeviceAvailability.Available, DeviceReadiness.Attention, "Secondary display closed.", now);
    }

    private static DeviceStatusSnapshot PreserveTime(
        DeviceStatusSnapshot next,
        IReadOnlyDictionary<string, DeviceStatusSnapshot> previous)
    {
        if (!previous.TryGetValue(next.DeviceId, out var old)) return next;
        return old.Availability == next.Availability && old.Readiness == next.Readiness && old.Detail == next.Detail
            ? next with { LastChangedAtUtc = old.LastChangedAtUtc }
            : next;
    }

    private static DeviceStatusSnapshot Unknown(string id, string name, DateTimeOffset now) =>
        new(id, name, DeviceAvailability.Available, DeviceReadiness.Unknown, "Status not checked.", now);
    private static DeviceStatusSnapshot Disabled(string id, string name, DateTimeOffset now) =>
        new(id, name, DeviceAvailability.Disabled, DeviceReadiness.Unknown, "Device simulation disabled.", now);
    private DateTimeOffset UtcNow()
    {
        var now = _timeProvider.GetUtcNow();
        if (now.Offset != TimeSpan.Zero) throw new InvalidOperationException("Device status timestamps must be UTC.");
        return now;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _scanner.StateChanged -= OnChanged;
        _printer.StateChanged -= OnChanged;
        _terminal.StateChanged -= OnChanged;
        _customerDisplay.StateChanged -= OnChanged;
        _displayTopologyMonitor.Changed -= OnChanged;
    }
}
