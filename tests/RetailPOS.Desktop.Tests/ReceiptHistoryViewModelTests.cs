using RetailPOS.Application.Checkout;
using RetailPOS.Application.Receipts;
using RetailPOS.Desktop.ViewModels;
using RetailPOS.Desktop.Workflow;
using RetailPOS.Domain.Payments;

namespace RetailPOS.Desktop.Tests;

public sealed class ReceiptHistoryViewModelTests
{
    private static readonly Guid FirstOrderId = Guid.Parse("75000000-0000-0000-0000-000000000001");
    private static readonly Guid SecondOrderId = Guid.Parse("75000000-0000-0000-0000-000000000002");
    private static readonly DateTimeOffset Now = new(2026, 7, 14, 2, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ActivateAsync_SelectsNewlyCompletedReceipt()
    {
        var state = new ReceiptPreviewState();
        state.Set(Receipt(SecondOrderId, "ORDER-002"));
        var query = new StubReceiptHistoryQuery(
            Page(FirstOrderId, SecondOrderId),
            id => Task.FromResult<ReceiptPreview?>(Receipt(id, id == FirstOrderId ? "ORDER-001" : "ORDER-002")));
        using var viewModel = ViewModel(query, state: state);

        await viewModel.ActivateAsync();
        await WaitUntilAsync(() => viewModel.HasDetail);

        Assert.Equal(SecondOrderId, viewModel.SelectedReceipt?.LocalOrderId);
        Assert.Equal("ORDER-002", viewModel.OrderNumber);
    }

    [Fact]
    public async Task CompletedPayment_DirectlyLoadsNewReceiptDespiteStaleFiltersAndMissingListRow()
    {
        var state = new ReceiptPreviewState();
        state.Set(Receipt(SecondOrderId, "ORDER-NEW"));
        var query = new StubReceiptHistoryQuery(
            Page(FirstOrderId),
            id => Task.FromResult<ReceiptPreview?>(
                id == SecondOrderId ? Receipt(SecondOrderId, "ORDER-NEW") : Receipt(FirstOrderId, "ORDER-OLD")));
        var navigator = Navigator();
        navigator.Navigate(CashierWorkflowScreen.ReceiptDetail);
        using var viewModel = ViewModel(query, state: state, navigator: navigator);
        viewModel.SelectedDate = new DateTime(2026, 7, 13);
        viewModel.SearchText = "ORDER-OLD";

        await viewModel.ActivateAsync();

        Assert.Equal(new DateTime(2026, 7, 14), viewModel.SelectedDate);
        Assert.Null(viewModel.SearchText);
        Assert.Equal("ORDER-NEW", viewModel.OrderNumber);
        Assert.Null(viewModel.SelectedReceipt);
        Assert.Equal([SecondOrderId], query.DetailRequests);
        Assert.Equal(new DateOnly(2026, 7, 14), query.LastRequest?.BusinessDate);
        Assert.Null(query.LastRequest?.SearchText);
    }

    [Fact]
    public async Task SelectingReceipt_IgnoresLateDetailFromOlderSelection()
    {
        var firstDetail = new TaskCompletionSource<ReceiptPreview?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var query = new StubReceiptHistoryQuery(
            Page(FirstOrderId, SecondOrderId),
            id => id == FirstOrderId
                ? firstDetail.Task
                : Task.FromResult<ReceiptPreview?>(Receipt(SecondOrderId, "ORDER-002")));
        using var viewModel = ViewModel(query);

        await viewModel.ActivateAsync();
        viewModel.SelectedReceipt = viewModel.Receipts.Single(item => item.LocalOrderId == SecondOrderId);
        await WaitUntilAsync(() => viewModel.OrderNumber == "ORDER-002");
        firstDetail.SetResult(Receipt(FirstOrderId, "ORDER-001"));
        await Task.Delay(20);

        Assert.Equal("ORDER-002", viewModel.OrderNumber);
        Assert.Equal(SecondOrderId, viewModel.SelectedReceipt?.LocalOrderId);
    }

    [Fact]
    public async Task PrintRetryFailure_ClearsPreviousSuccessWithoutChangingDetail()
    {
        var printer = new StubReceiptPrinter();
        var query = new StubReceiptHistoryQuery(
            Page(FirstOrderId),
            id => Task.FromResult<ReceiptPreview?>(Receipt(id, "ORDER-001")));
        using var viewModel = ViewModel(query, printer);
        await viewModel.ActivateAsync();
        await WaitUntilAsync(() => viewModel.HasDetail);

        await viewModel.PrintCommand.ExecuteAsync(null);
        printer.Succeeds = false;
        await viewModel.PrintCommand.ExecuteAsync(null);

        Assert.Null(viewModel.StatusMessage);
        Assert.Equal("Printer unavailable.", viewModel.ErrorMessage);
        Assert.Equal("ORDER-001", viewModel.OrderNumber);
        Assert.Equal(2, printer.RequestCount);
    }

    private static ReceiptHistoryViewModel ViewModel(
        IReceiptHistoryQuery query,
        IReceiptPrinter? printer = null,
        ReceiptPreviewState? state = null,
        CashierWorkflowNavigator? navigator = null) => new(
            query,
            printer ?? new StubReceiptPrinter(),
            new StubClock(Now),
            state ?? new ReceiptPreviewState(),
            navigator ?? Navigator());

    private static ReceiptHistoryPage Page(params Guid[] orderIds) => new(
        orderIds.Select((id, index) => new ReceiptHistorySummary(
            id,
            $"ORDER-{index + 1:000}",
            Now.AddMinutes(-index),
            index % 2 == 0 ? PaymentMethod.Card : PaymentMethod.Cash,
            1000m + index,
            "Cashier A")).ToArray(),
        HasMore: false);

    private static ReceiptPreview Receipt(Guid id, string orderNumber) => new(
        "Retail Store",
        "Local POS Terminal",
        orderNumber,
        "Cashier A",
        "Register 01",
        Now,
        new DateOnly(2026, 7, 14),
        [new ReceiptPreviewLine("Product", 1000m, 1, 1000m, 0m, 1000m)],
        [new ReceiptPreviewPayment(PaymentMethod.Card, 1000m, "APP-001")],
        1000m,
        0m,
        1000m,
        "receipt",
        id);

    private static CashierWorkflowNavigator Navigator()
    {
        var registry = new CashierWorkflowScreenRegistry();
        registry.Register(Enum.GetValues<CashierWorkflowScreen>());
        var navigator = new CashierWorkflowNavigator(registry);
        navigator.Reset(CashierWorkflowScreen.Register);
        return navigator;
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var timeout = DateTime.UtcNow.AddSeconds(2);
        while (!condition())
        {
            if (DateTime.UtcNow >= timeout)
            {
                throw new TimeoutException("ViewModel state did not settle in time.");
            }

            await Task.Delay(10);
        }
    }

    private sealed class StubReceiptHistoryQuery(
        ReceiptHistoryPage page,
        Func<Guid, Task<ReceiptPreview?>> detailFactory) : IReceiptHistoryQuery
    {
        public ReceiptHistoryRequest? LastRequest { get; private set; }
        public List<Guid> DetailRequests { get; } = [];

        public Task<ReceiptHistoryPage> SearchAsync(
            ReceiptHistoryRequest request,
            CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.FromResult(page);
        }

        public Task<ReceiptPreview?> GetDetailAsync(
            Guid localOrderId,
            CancellationToken cancellationToken = default)
        {
            DetailRequests.Add(localOrderId);
            return detailFactory(localOrderId);
        }
    }

    private sealed class StubReceiptPrinter : IReceiptPrinter
    {
        public bool Succeeds { get; set; } = true;
        public int RequestCount { get; private set; }

        public Task<ReceiptPrintResult> PrintAsync(
            ReceiptPreview receipt,
            CancellationToken cancellationToken = default)
        {
            RequestCount++;
            return Task.FromResult(new ReceiptPrintResult(
                Succeeds ? ReceiptPrintOutcome.Printed : ReceiptPrintOutcome.Disconnected,
                Succeeds ? Now : null,
                Succeeds ? "Printed." : "Printer unavailable."));
        }
    }

    private sealed class StubClock(DateTimeOffset utcNow) : ICheckoutClock
    {
        public DateTimeOffset UtcNow => utcNow;
    }
}
