using RetailPOS.Application.Receipts;
using RetailPOS.Desktop.ViewModels;
using RetailPOS.Domain.Payments;
using RetailPOS.Infrastructure.Devices;

namespace RetailPOS.Desktop.Tests;

public sealed class ReceiptPrinterSimulatorViewModelTests
{
    [Fact]
    public async Task PendingRequest_IsDisplayedAndCanBeCompleted()
    {
        using var printer = new SimulatedReceiptPrinter(TimeProvider.System);
        using var viewModel = new ReceiptPrinterSimulatorViewModel(printer);

        var printing = printer.PrintAsync(Receipt());

        Assert.Contains("LOCAL-001", viewModel.PendingRequestTitle);
        Assert.Contains("Cola", viewModel.PrintableText);
        Assert.True(viewModel.RespondCommand.CanExecute(null));
        viewModel.SelectedOutcome = viewModel.Outcomes.Single(x => x.Outcome == ReceiptPrintOutcome.Printed);
        viewModel.RespondCommand.Execute(null);

        Assert.Equal(ReceiptPrintOutcome.Printed, (await printing).Outcome);
        Assert.Equal("No pending print request", viewModel.PendingRequestTitle);
        Assert.Single(viewModel.RecentRequests);
    }

    [Fact]
    public void Busy_IsNotAnOperatorResponseOption()
    {
        using var printer = new SimulatedReceiptPrinter(TimeProvider.System);
        using var viewModel = new ReceiptPrinterSimulatorViewModel(printer);

        Assert.DoesNotContain(viewModel.Outcomes, option => option.Outcome == ReceiptPrintOutcome.Busy);
    }

    [Fact]
    public void ConnectionCommandsTrackControlState()
    {
        using var printer = new SimulatedReceiptPrinter(TimeProvider.System);
        using var viewModel = new ReceiptPrinterSimulatorViewModel(printer);

        viewModel.DisconnectCommand.Execute(null);
        Assert.False(viewModel.IsConnected);
        viewModel.ConnectCommand.Execute(null);
        Assert.True(viewModel.IsConnected);
    }

    private static ReceiptPreview Receipt() => new(
        "Retail Store", "Local POS Terminal", "LOCAL-001", "Cashier A", "Register 01",
        DateTimeOffset.UtcNow, DateOnly.FromDateTime(DateTime.Today),
        [new ReceiptPreviewLine("Cola", 1800m, 1, 1800m, 0m, 1800m)],
        [new ReceiptPreviewPayment(PaymentMethod.Card, 1800m, "APP-001")],
        1800m, 0m, 1800m, "Retail Store\nCola x1\nTotal 1,800");
}
