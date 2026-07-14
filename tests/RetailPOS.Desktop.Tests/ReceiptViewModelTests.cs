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
    public void CashPayment_BindsTenderAndChangeText()
    {
        var payment = new ReceiptPaymentViewModel(
            new ReceiptPreviewPayment(PaymentMethod.Cash, 3400m, "APP-CASH", 5000m, 1600m));

        Assert.True(payment.HasCashTenderDetails);
        Assert.Equal("Tendered 5,000 KRW", payment.CashTenderedText);
        Assert.Equal("Change 1,600 KRW", payment.ChangeText);
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
        Assert.True(viewModel.IsSuccessStatus);
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
        Assert.False(viewModel.IsSuccessStatus);
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

    [Fact]
    public async Task Dispose_CancelsDelayedPrintAndIgnoresLateState()
    {
        var state = new ReceiptPreviewState();
        state.Set(Receipt());
        var printer = new BlockingReceiptPrinter();
        var viewModel = new ReceiptViewModel(printer, state);
        var printing = viewModel.PrintCommand.ExecuteAsync(null);
        await printer.Started.Task;

        Assert.Equal(
            "Print request sent. Waiting for simulator response...",
            viewModel.StatusMessage);
        Assert.False(viewModel.IsSuccessStatus);
        Assert.True(viewModel.IsBusy);

        viewModel.Dispose();
        await printing;

        Assert.True(printer.WasCancelled);
        Assert.Null(viewModel.StatusMessage);
        Assert.Null(viewModel.ErrorMessage);
        Assert.False(viewModel.PrintCommand.CanExecute(null));
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
                Succeeds ? ReceiptPrintOutcome.Printed : ReceiptPrintOutcome.Failed,
                Succeeds ? IssuedAtUtc : null,
                Succeeds
                    ? "Receipt printed successfully."
                    : "Receipt could not be printed. Try again."));
        }
    }

    private sealed class BlockingReceiptPrinter : IReceiptPrinter
    {
        public TaskCompletionSource Started { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool WasCancelled { get; private set; }

        public async Task<ReceiptPrintResult> PrintAsync(
            ReceiptPreview receipt,
            CancellationToken cancellationToken = default)
        {
            Started.SetResult();
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                WasCancelled = true;
                throw;
            }

            throw new InvalidOperationException("Unreachable");
        }
    }
}
