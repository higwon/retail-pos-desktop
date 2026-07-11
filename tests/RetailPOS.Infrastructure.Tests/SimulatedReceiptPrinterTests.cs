using RetailPOS.Application.Receipts;
using RetailPOS.Domain.Payments;
using RetailPOS.Infrastructure.Devices;

namespace RetailPOS.Infrastructure.Tests;

public sealed class SimulatedReceiptPrinterTests
{
    private static readonly DateTimeOffset PrintedAtUtc =
        new(2026, 7, 10, 1, 2, 3, TimeSpan.Zero);

    [Fact]
    public async Task PrintAsync_WaitsForOperatorAndExposesPrintableRequest()
    {
        using var printer = new SimulatedReceiptPrinter(new StubTimeProvider(PrintedAtUtc));

        var printing = printer.PrintAsync(Receipt());
        var pending = printer.PendingRequest;

        Assert.NotNull(pending);
        Assert.False(printing.IsCompleted);
        Assert.Equal("LOCAL-001", pending.BusinessIdentity);
        Assert.Equal("Retail Store", pending.Payload.Receipt.StoreName);
        Assert.Equal("Register 01", pending.Payload.Receipt.RegisterName);
        Assert.Equal("Cashier A", pending.Payload.Receipt.CashierName);
        Assert.Contains("Cola", pending.Payload.PrintableText);
        Assert.True(printer.Respond(pending.RequestId, ReceiptPrintOutcome.Printed));

        var result = await printing;
        Assert.True(result.Succeeded);
        Assert.Equal(PrintedAtUtc, result.PrintedAtUtc);
        Assert.Equal(ReceiptPrinterOperationalState.Ready, printer.OperationalState);
    }

    [Theory]
    [InlineData(ReceiptPrintOutcome.PaperOut)]
    [InlineData(ReceiptPrintOutcome.CoverOpen)]
    [InlineData(ReceiptPrintOutcome.Timeout)]
    [InlineData(ReceiptPrintOutcome.Failed)]
    public async Task OperatorFaultResponse_IsReturnedAsTypedOutcome(ReceiptPrintOutcome outcome)
    {
        using var printer = new SimulatedReceiptPrinter(TimeProvider.System);
        var printing = printer.PrintAsync(Receipt());

        Assert.True(printer.Respond(printer.PendingRequest!.RequestId, outcome));
        var result = await printing;

        Assert.Equal(outcome, result.Outcome);
        Assert.Null(result.PrintedAtUtc);
        Assert.Equal(ReceiptPrinterOperationalState.Faulted, printer.OperationalState);
    }

    [Fact]
    public async Task ConcurrentRequest_IsAutomaticallyRejectedAsBusy()
    {
        using var printer = new SimulatedReceiptPrinter(TimeProvider.System);
        using var cancellation = new CancellationTokenSource();
        var first = printer.PrintAsync(Receipt(), cancellation.Token);

        var second = await printer.PrintAsync(Receipt());

        Assert.Equal(ReceiptPrintOutcome.Busy, second.Outcome);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            printer.Respond(printer.PendingRequest!.RequestId, ReceiptPrintOutcome.Busy));
        cancellation.Cancel();
        Assert.Equal(ReceiptPrintOutcome.Cancelled, (await first).Outcome);
    }

    [Fact]
    public async Task Retry_UsesNewRequestIdAndPreservesReceiptIdentity()
    {
        using var printer = new SimulatedReceiptPrinter(TimeProvider.System);
        var first = printer.PrintAsync(Receipt());
        var firstRequest = printer.PendingRequest!;
        printer.Respond(firstRequest.RequestId, ReceiptPrintOutcome.PaperOut);
        Assert.Equal(ReceiptPrintOutcome.PaperOut, (await first).Outcome);

        printer.Reset();
        var retry = printer.PrintAsync(Receipt());
        var retryRequest = printer.PendingRequest!;

        Assert.NotEqual(firstRequest.RequestId, retryRequest.RequestId);
        Assert.Equal(firstRequest.BusinessIdentity, retryRequest.BusinessIdentity);
        printer.Respond(retryRequest.RequestId, ReceiptPrintOutcome.Printed);
        Assert.Equal(ReceiptPrintOutcome.Printed, (await retry).Outcome);
    }

    [Fact]
    public async Task LateOrDuplicateResponse_IsRejected()
    {
        using var printer = new SimulatedReceiptPrinter(TimeProvider.System);
        var printing = printer.PrintAsync(Receipt());
        var requestId = printer.PendingRequest!.RequestId;

        Assert.True(printer.Respond(requestId, ReceiptPrintOutcome.Printed));
        Assert.False(printer.Respond(requestId, ReceiptPrintOutcome.Failed));
        Assert.Equal(ReceiptPrintOutcome.Printed, (await printing).Outcome);
    }

    [Fact]
    public async Task Disconnect_CompletesPendingRequestAsDisconnected()
    {
        using var printer = new SimulatedReceiptPrinter(TimeProvider.System);
        var printing = printer.PrintAsync(Receipt());

        printer.Disconnect();

        Assert.Equal(ReceiptPrintOutcome.Disconnected, (await printing).Outcome);
        Assert.Null(printer.PendingRequest);
        Assert.Equal(ReceiptPrinterOperationalState.Disconnected, printer.OperationalState);
    }

    [Fact]
    public async Task Reset_WhileRequestPendingIsRejected()
    {
        using var printer = new SimulatedReceiptPrinter(TimeProvider.System);
        using var cancellation = new CancellationTokenSource();
        var printing = printer.PrintAsync(Receipt(), cancellation.Token);

        Assert.Throws<InvalidOperationException>(printer.Reset);
        cancellation.Cancel();
        await printing;
    }

    private static ReceiptPreview Receipt() => new(
        "Retail Store", "Local POS Terminal", "LOCAL-001", "Cashier A", "Register 01",
        PrintedAtUtc.AddMinutes(-1), new DateOnly(2026, 7, 10),
        [new ReceiptPreviewLine("Cola", 1800m, 1, 1800m, 0m, 1800m)],
        [new ReceiptPreviewPayment(PaymentMethod.Card, 1800m, "APP-001")],
        1800m, 0m, 1800m, "Retail Store\nCola x1\nTotal 1,800");

    private sealed class StubTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
