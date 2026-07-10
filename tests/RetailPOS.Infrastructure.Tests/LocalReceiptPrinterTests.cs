using RetailPOS.Application.Receipts;
using RetailPOS.Domain.Payments;
using RetailPOS.Infrastructure.Devices;

namespace RetailPOS.Infrastructure.Tests;

public sealed class LocalReceiptPrinterTests
{
    private static readonly DateTimeOffset PrintedAtUtc =
        new(2026, 7, 10, 1, 2, 3, TimeSpan.Zero);

    [Fact]
    public async Task PrintAsync_ReturnsSuccessfulResult()
    {
        var printer = new LocalReceiptPrinter(new StubTimeProvider(PrintedAtUtc));

        var result = await printer.PrintAsync(Receipt());

        Assert.True(result.Succeeded);
        Assert.Equal(PrintedAtUtc, result.PrintedAtUtc);
        Assert.Equal("Receipt printed successfully.", result.Message);
    }

    [Fact]
    public async Task PrintAsync_ReturnsUserSafeFailureResult()
    {
        var printer = new LocalReceiptPrinter(
            new StubTimeProvider(PrintedAtUtc),
            ReceiptPrintSimulationMode.Fail);

        var result = await printer.PrintAsync(Receipt());

        Assert.False(result.Succeeded);
        Assert.Null(result.PrintedAtUtc);
        Assert.Equal("Receipt could not be printed. Try again.", result.Message);
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
