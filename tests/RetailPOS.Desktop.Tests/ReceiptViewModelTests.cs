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

        var viewModel = new ReceiptViewModel(new StubReceiptService(), state);

        Assert.True(viewModel.HasReceipt);
        Assert.Equal("LOCAL-001", viewModel.OrderNumber);
        Assert.Equal("3,400 KRW", viewModel.TotalAmount);
        Assert.Single(viewModel.Lines);
        Assert.Single(viewModel.Payments);
    }

    [Fact]
    public async Task PrintCommand_UsesLocalPrintSimulation()
    {
        var state = new ReceiptPreviewState();
        state.Set(Receipt());
        var service = new StubReceiptService();
        var viewModel = new ReceiptViewModel(service, state);

        await viewModel.PrintCommand.ExecuteAsync(null);

        Assert.NotNull(service.PrintedReceipt);
        Assert.Equal("Receipt print simulated.", viewModel.StatusMessage);
    }

    [Fact]
    public void ReopeningReceiptPreview_DoesNotConsumeReceiptState()
    {
        var state = new ReceiptPreviewState();
        state.Set(Receipt());

        _ = new ReceiptViewModel(new StubReceiptService(), state);
        var reopened = new ReceiptViewModel(new StubReceiptService(), state);

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

    private sealed class StubReceiptService : IReceiptService
    {
        public ReceiptPreview? PrintedReceipt { get; private set; }

        public Task<ReceiptPreview> GenerateAsync(
            Guid localOrderId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Receipt());

        public Task<ReceiptPrintResult> PrintAsync(
            ReceiptPreview receipt,
            CancellationToken cancellationToken = default)
        {
            PrintedReceipt = receipt;
            return Task.FromResult(new ReceiptPrintResult(
                true,
                IssuedAtUtc,
                "Receipt print simulated."));
        }
    }
}
