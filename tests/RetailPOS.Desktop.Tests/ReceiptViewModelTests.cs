using RetailPOS.Application.Receipts;
using RetailPOS.Desktop.ViewModels;
using RetailPOS.Domain.Payments;

namespace RetailPOS.Desktop.Tests;

public sealed class ReceiptViewModelTests
{
    private static readonly DateTimeOffset IssuedAtUtc = new(2026, 7, 8, 1, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Constructor_BindsCurrentReceiptPreview()
    {
        var state = new ReceiptPreviewState();
        state.Set(Receipt());

        var viewModel = new ReceiptViewModel(new StubReceiptPrinter(), state);

        Assert.True(viewModel.HasReceipt);
        Assert.Equal("LOCAL-001", viewModel.OrderNumber);
        Assert.Equal("3,400 KRW", viewModel.TotalAmount);
        Assert.Single(viewModel.Lines);
        Assert.Single(viewModel.Payments);
    }

    [Fact]
    public async Task PrintCommand_ShowsPrinterSuccessMessage()
    {
        var state = new ReceiptPreviewState();
        state.Set(Receipt());
        var printer = new StubReceiptPrinter();
        var viewModel = new ReceiptViewModel(printer, state);

        await viewModel.PrintCommand.ExecuteAsync(null);

        Assert.NotNull(printer.PrintedReceipt);
        Assert.Equal("Receipt printed successfully.", viewModel.StatusMessage);
        Assert.Null(viewModel.ErrorMessage);
    }

    [Fact]
    public async Task PrintCommand_ShowsUserSafePrinterFailureMessage()
    {
        var state = new ReceiptPreviewState();
        state.Set(Receipt());
        var printer = new StubReceiptPrinter(succeeds: false);
        var viewModel = new ReceiptViewModel(printer, state);

        await viewModel.PrintCommand.ExecuteAsync(null);

        Assert.NotNull(printer.PrintedReceipt);
        Assert.Null(viewModel.StatusMessage);
        Assert.Equal("Receipt could not be printed. Try again.", viewModel.ErrorMessage);
    }

    [Fact]
    public async Task PrintCommand_ClearsPreviousSuccessMessageWhenRetryFails()
    {
        var state = new ReceiptPreviewState();
        state.Set(Receipt());
        var printer = new StubReceiptPrinter();
        var viewModel = new ReceiptViewModel(printer, state);

        await viewModel.PrintCommand.ExecuteAsync(null);
        printer.Succeeds = false;
        await viewModel.PrintCommand.ExecuteAsync(null);

        Assert.Null(viewModel.StatusMessage);
        Assert.Equal("Receipt could not be printed. Try again.", viewModel.ErrorMessage);
    }

    [Fact]
    public async Task PrintCommand_ClearsPreviousSuccessMessageWhenRetryThrows()
    {
        var state = new ReceiptPreviewState();
        state.Set(Receipt());
        var printer = new StubReceiptPrinter();
        var viewModel = new ReceiptViewModel(printer, state);

        await viewModel.PrintCommand.ExecuteAsync(null);
        printer.ThrowOnPrint = true;
        await viewModel.PrintCommand.ExecuteAsync(null);

        Assert.Null(viewModel.StatusMessage);
        Assert.Equal(
            "Receipt could not be printed. The order is already completed; try again.",
            viewModel.ErrorMessage);
    }

    [Fact]
    public void ReopeningReceiptPreview_DoesNotConsumeReceiptState()
    {
        var state = new ReceiptPreviewState();
        state.Set(Receipt());

        _ = new ReceiptViewModel(new StubReceiptPrinter(), state);
        var reopened = new ReceiptViewModel(new StubReceiptPrinter(), state);

        Assert.True(reopened.HasReceipt);
        Assert.Equal("LOCAL-001", reopened.OrderNumber);
    }

    private static ReceiptPreview Receipt() => new(
        "Retail Store",
        "Local POS Terminal",
        "LOCAL-001",
        "Cashier A",
        "Register 01",
        IssuedAtUtc,
        new DateOnly(2026, 7, 8),
        [new ReceiptPreviewLine("Cola", 1800m, 2, 3600m, 200m, 3400m)],
        [new ReceiptPreviewPayment(PaymentMethod.Card, 3400m, "APP-001")],
        3600m,
        200m,
        3400m,
        "receipt");

    private sealed class StubReceiptPrinter(bool succeeds = true) : IReceiptPrinter
    {
        public ReceiptPreview? PrintedReceipt { get; private set; }
        public bool Succeeds { get; set; } = succeeds;
        public bool ThrowOnPrint { get; set; }

        public Task<ReceiptPrintResult> PrintAsync(
            ReceiptPreview receipt,
            CancellationToken cancellationToken = default)
        {
            if (ThrowOnPrint)
            {
                throw new InvalidOperationException("Simulated printer failure.");
            }

            PrintedReceipt = receipt;
            return Task.FromResult(new ReceiptPrintResult(
                Succeeds,
                Succeeds ? IssuedAtUtc : null,
                Succeeds
                    ? "Receipt printed successfully."
                    : "Receipt could not be printed. Try again."));
        }
    }
}
