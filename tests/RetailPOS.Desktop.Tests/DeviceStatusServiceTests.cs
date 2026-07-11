using Microsoft.Extensions.Options;
using RetailPOS.Application.Devices;
using RetailPOS.Desktop.DeviceSimulation;
using RetailPOS.Infrastructure.Devices;
using System.Drawing;

namespace RetailPOS.Desktop.Tests;

public sealed class DeviceStatusServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 11, 5, 0, 0, TimeSpan.Zero);

    [Fact]
    public void InitialUnknown_RefreshAndAutomaticEventsAreDistinctAndUtc()
    {
        using var scanner = new SimulatedBarcodeScanner();
        using var printer = new SimulatedReceiptPrinter(TimeProvider.System);
        using var terminal = new SimulatedPaymentTerminal(TimeProvider.System);
        var display = DisplayHost(hasSecondary: true);
        var clock = new MutableTimeProvider(Now);
        using var service = Service(scanner, printer, terminal, display, clock, enabled: true);

        Assert.All(service.Current.Devices, device => Assert.Equal(DeviceReadiness.Unknown, device.Readiness));
        Assert.All(service.Current.Devices, device => Assert.Equal(DeviceAvailability.Available, device.Availability));

        service.Refresh();
        Assert.Equal("Devices: 1 Attention", service.Current.Summary);
        Assert.Equal(DeviceReadiness.Ready, Device(service, "scanner").Readiness);
        Assert.Equal(DeviceReadiness.Attention, Device(service, "display").Readiness);
        Assert.Equal(Now, Device(service, "scanner").LastChangedAtUtc);

        clock.UtcNow = Now.AddMinutes(1);
        scanner.Disconnect();
        Assert.Equal(DeviceAvailability.Unavailable, Device(service, "scanner").Availability);
        Assert.Equal(Now.AddMinutes(1), Device(service, "scanner").LastChangedAtUtc);
        Assert.Equal(TimeSpan.Zero, Device(service, "scanner").LastChangedAtUtc.Offset);
    }

    [Fact]
    public void DisabledSimulator_IsDifferentFromUnavailableDevice()
    {
        using var scanner = new SimulatedBarcodeScanner();
        using var printer = new SimulatedReceiptPrinter(TimeProvider.System);
        using var terminal = new SimulatedPaymentTerminal(TimeProvider.System);
        var display = DisplayHost(hasSecondary: false);
        using var disabled = Service(scanner, printer, terminal, display, new MutableTimeProvider(Now), enabled: false);

        disabled.Refresh();

        Assert.All(disabled.Current.Devices, device => Assert.Equal(DeviceAvailability.Disabled, device.Availability));
        Assert.All(disabled.Current.Devices, device => Assert.Contains("disabled", device.Detail, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Dispose_UnsubscribesFromAdapterEvents()
    {
        using var scanner = new SimulatedBarcodeScanner();
        using var printer = new SimulatedReceiptPrinter(TimeProvider.System);
        using var terminal = new SimulatedPaymentTerminal(TimeProvider.System);
        var display = DisplayHost(hasSecondary: true);
        var service = Service(scanner, printer, terminal, display, new MutableTimeProvider(Now), enabled: true);
        service.Refresh();
        var changes = 0;
        service.Changed += (_, _) => changes++;
        service.Dispose();

        scanner.Disconnect();

        Assert.Equal(0, changes);
    }

    private static DeviceStatusSnapshot Device(DeviceStatusService service, string id) =>
        service.Current.Devices.Single(device => device.DeviceId == id);

    private static DeviceStatusService Service(
        SimulatedBarcodeScanner scanner,
        SimulatedReceiptPrinter printer,
        SimulatedPaymentTerminal terminal,
        CustomerDisplayHost display,
        TimeProvider clock,
        bool enabled)
    {
        display.RefreshTargets();
        return new DeviceStatusService(scanner, printer, terminal, display,
            new StubDisplayTargetProvider(display.Targets),
            Options.Create(new DeviceSimulationOptions { Enabled = enabled }), clock);
    }

    private static CustomerDisplayHost DisplayHost(bool hasSecondary)
    {
        var targets = new List<DisplayTarget>
        {
            new("primary", "Primary", new Rectangle(0, 0, 1920, 1080), true)
        };
        if (hasSecondary) targets.Add(new("secondary", "Secondary", new Rectangle(1920, 0, 1920, 1080), false));
        return new CustomerDisplayHost(new StubDisplayTargetProvider(targets), () => new StubDisplayWindow());
    }

    private sealed class StubDisplayTargetProvider(IReadOnlyList<DisplayTarget> targets) : IDisplayTargetProvider
    {
        public IReadOnlyList<DisplayTarget> GetTargets() => targets;
    }

    private sealed class StubDisplayWindow : ICustomerDisplayWindow
    {
        public bool IsVisible { get; private set; }
        public event EventHandler? Closed;
        public void ShowOn(DisplayTarget target) => IsVisible = true;
        public void MoveTo(DisplayTarget target) { }
        public void Activate() { }
        public void Close() { IsVisible = false; Closed?.Invoke(this, EventArgs.Empty); }
    }

    private sealed class MutableTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = utcNow;
        public override DateTimeOffset GetUtcNow() => UtcNow;
    }
}
