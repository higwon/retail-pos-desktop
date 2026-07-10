using RetailPOS.Application.Receipts;
using RetailPOS.Domain.Payments;
using RetailPOS.Infrastructure.Devices;

namespace RetailPOS.Infrastructure.Tests;

public sealed class SimulatedReceiptPrinterTests
{
    private static readonly DateTimeOffset PrintedAtUtc =
        new(2026, 7, 10, 1, 2, 3, TimeSpan.Zero);

    [Fact]
    public async Task PrintAsync_DefaultSettingsPrintReceiptWithUtcTimestamp()
    {
        var printer = new SimulatedReceiptPrinter(new StubTimeProvider(PrintedAtUtc));

        var result = await printer.PrintAsync(Receipt());

        Assert.True(result.Succeeded);
        Assert.Equal(ReceiptPrintOutcome.Printed, result.Outcome);
        Assert.Equal(PrintedAtUtc, result.PrintedAtUtc);
        Assert.Equal(ReceiptPrinterOperationalState.Ready, printer.OperationalState);
    }

    [Theory]
    [InlineData(ReceiptPrintOutcome.PaperOut)]
    [InlineData(ReceiptPrintOutcome.CoverOpen)]
    [InlineData(ReceiptPrintOutcome.Timeout)]
    [InlineData(ReceiptPrintOutcome.Failed)]
    public async Task PrintAsync_FaultOutcomeIsTypedAndLeavesPrinterFaulted(
        ReceiptPrintOutcome outcome)
    {
        var printer = new SimulatedReceiptPrinter(TimeProvider.System);
        printer.ConfigureNext(new ReceiptPrinterSimulationSettings(outcome, TimeSpan.Zero));

        var result = await printer.PrintAsync(Receipt());

        Assert.Equal(outcome, result.Outcome);
        Assert.False(result.Succeeded);
        Assert.Null(result.PrintedAtUtc);
        Assert.Equal(ReceiptPrinterOperationalState.Faulted, printer.OperationalState);
    }

    [Fact]
    public async Task PrintAsync_ConcurrentRequestReturnsBusy()
    {
        var printer = new SimulatedReceiptPrinter(TimeProvider.System);
        printer.ConfigureNext(new ReceiptPrinterSimulationSettings(
            ReceiptPrintOutcome.Printed,
            TimeSpan.FromMinutes(1)));
        using var cancellation = new CancellationTokenSource();
        var first = printer.PrintAsync(Receipt(), cancellation.Token);
        Assert.Equal(ReceiptPrinterOperationalState.Printing, printer.OperationalState);

        var second = await printer.PrintAsync(Receipt());

        Assert.Equal(ReceiptPrintOutcome.Busy, second.Outcome);
        cancellation.Cancel();
        Assert.Equal(ReceiptPrintOutcome.Cancelled, (await first).Outcome);
    }

    [Fact]
    public async Task Disconnect_CancelsDelayedPrintAndLeavesPrinterDisconnected()
    {
        var printer = new SimulatedReceiptPrinter(TimeProvider.System);
        printer.ConfigureNext(new ReceiptPrinterSimulationSettings(
            ReceiptPrintOutcome.Printed,
            TimeSpan.FromMinutes(1)));
        var printing = printer.PrintAsync(Receipt());

        printer.Disconnect();
        var result = await printing;

        Assert.Equal(ReceiptPrintOutcome.Disconnected, result.Outcome);
        Assert.Equal(ReceiptPrinterConnectionState.Disconnected, printer.ConnectionState);
        Assert.Equal(ReceiptPrinterOperationalState.Disconnected, printer.OperationalState);
    }

    [Fact]
    public async Task ResetAfterFaultAllowsRetry()
    {
        var printer = new SimulatedReceiptPrinter(TimeProvider.System);
        printer.ConfigureNext(new ReceiptPrinterSimulationSettings(
            ReceiptPrintOutcome.PaperOut,
            TimeSpan.Zero));
        Assert.Equal(ReceiptPrintOutcome.PaperOut, (await printer.PrintAsync(Receipt())).Outcome);

        printer.Reset();
        var retry = await printer.PrintAsync(Receipt());

        Assert.Equal(ReceiptPrinterOperationalState.Ready, printer.OperationalState);
        Assert.Equal(ReceiptPrintOutcome.Printed, retry.Outcome);
    }

    [Fact]
    public void ConfigureNext_UnknownOutcomeFailsClosed()
    {
        var printer = new SimulatedReceiptPrinter(TimeProvider.System);

        Assert.Throws<ArgumentOutOfRangeException>(() => printer.ConfigureNext(
            new ReceiptPrinterSimulationSettings((ReceiptPrintOutcome)999, TimeSpan.Zero)));
    }

    private static ReceiptPreview Receipt() => new(
        "Retail Store",
        "Local POS Terminal",
        "LOCAL-001",
        "Cashier A",
        "Register 01",
        PrintedAtUtc.AddMinutes(-1),
        new DateOnly(2026, 7, 10),
        [new ReceiptPreviewLine("Cola", 1800m, 1, 1800m, 0m, 1800m)],
        [new ReceiptPreviewPayment(PaymentMethod.Card, 1800m, "APP-001")],
        1800m,
        0m,
        1800m,
        "receipt");

    private sealed class StubTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
