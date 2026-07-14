using System.Drawing;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RetailPOS.Application.Authentication;
using RetailPOS.Application.Checkout;
using RetailPOS.Application.Persistence;
using RetailPOS.Application.Receipts;
using RetailPOS.Desktop.DeviceSimulation;
using RetailPOS.Desktop.ViewModels;
using RetailPOS.Desktop.Workflow;
using RetailPOS.Domain.Products;
using RetailPOS.Infrastructure.Devices;

namespace RetailPOS.Desktop.Tests;

public sealed class SessionSignOutCoordinatorTests
{
    [Fact]
    public async Task SignOut_CancelsPendingWorkStopsScannerClearsStateAndAllowsCleanRelogin()
    {
        var product = Product();
        var checkout = new CheckoutSession();
        checkout.AddProduct(product);
        var receiptState = new ReceiptPreviewState();
        receiptState.Set(Receipt());
        var session = new CurrentSessionContext();
        session.SignIn(Cashier("100001", "First Cashier"));

        using var scanner = new SimulatedBarcodeScanner();
        using var scannerCoordinator = new BarcodeScannerCoordinator(
            scanner,
            new ProductGridViewModel(new StubProductRepository(product), checkout),
            new ImmediateDispatcher(),
            NullLogger<BarcodeScannerCoordinator>.Instance);
        scannerCoordinator.Start();

        var displayWindow = new TrackingDisplayWindow();
        using var displayHost = new CustomerDisplayHost(
            new SecondaryDisplayProvider(),
            () => displayWindow);
        displayHost.Open("secondary");

        var navigator = CreateNavigator();
        navigator.Reset(CashierWorkflowScreen.Register);
        using var receiptHistory = new ReceiptHistoryViewModel(
            new EmptyReceiptHistoryQuery(),
            new NoopReceiptPrinter(),
            new StubClock(),
            receiptState,
            navigator)
        {
            SearchText = "previous cashier"
        };
        using var simulatorHost = new DeviceSimulatorWindowHost(
            () => throw new InvalidOperationException("Simulator should not open in this test."),
            Options.Create(new DeviceSimulationOptions { Enabled = false }));
        var workflow = new SessionWorkflowLifecycle(
            scannerCoordinator,
            new NoopPaymentCoordinator(),
            receiptHistory,
            simulatorHost,
            displayHost);
        var coordinator = new SessionSignOutCoordinator(
            workflow,
            checkout,
            receiptState,
            session,
            navigator);

        coordinator.SignOut();

        Assert.False(displayHost.IsOpen);
        Assert.True(displayWindow.WasClosed);
        Assert.Null(receiptHistory.SearchText);
        Assert.False(receiptHistory.HasDetail);
        Assert.True(checkout.Snapshot.IsEmpty);
        Assert.False(receiptState.HasReceipt);
        Assert.False(session.IsSignedIn);
        Assert.Equal(CashierWorkflowScreen.Login, navigator.Current);
        Assert.False(navigator.CanGoBack);

        await scanner.EmitAsync(product.Barcode);
        await Task.Delay(50);
        Assert.True(checkout.Snapshot.IsEmpty);

        var secondCashier = Cashier("100002", "Second Cashier");
        session.SignIn(secondCashier);

        Assert.Equal(secondCashier, session.Current);
        Assert.True(checkout.Snapshot.IsEmpty);
        Assert.False(receiptState.HasReceipt);
    }

    [Fact]
    public void SignOut_UsesDocumentedTeardownOrderBeforeClearingCheckoutAndSession()
    {
        var checkout = new CheckoutSession();
        checkout.AddProduct(Product());
        var receiptState = new ReceiptPreviewState();
        receiptState.Set(Receipt());
        var session = new CurrentSessionContext();
        session.SignIn(Cashier("100001", "First Cashier"));
        var lifecycle = new OrderingLifecycle(checkout, receiptState, session);
        var navigator = CreateNavigator();
        navigator.Reset(CashierWorkflowScreen.Register);
        var coordinator = new SessionSignOutCoordinator(
            lifecycle,
            checkout,
            receiptState,
            session,
            navigator);

        coordinator.SignOut();

        Assert.Equal(
            ["payment", "receipt", "simulator", "display", "scanner"],
            lifecycle.Calls);
        Assert.All(lifecycle.ReceiptWasCleared, Assert.True);
        Assert.All(lifecycle.CheckoutWasPresent, Assert.True);
        Assert.All(lifecycle.SessionWasPresent, Assert.True);
        Assert.True(checkout.Snapshot.IsEmpty);
        Assert.False(session.IsSignedIn);
        Assert.Equal(CashierWorkflowScreen.Login, navigator.Current);
    }

    private static Product Product() => new(
        Guid.NewGuid(), "SKU-1", "8800000000001", "Cleanser", "Skin Care", 12000m);

    private static CashierWorkflowNavigator CreateNavigator()
    {
        var registry = new CashierWorkflowScreenRegistry();
        registry.Register([
            CashierWorkflowScreen.Login,
            CashierWorkflowScreen.Register]);
        return new(registry);
    }

    private static CashierSession Cashier(string employeeCode, string name) => new(
        Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), employeeCode, name, DateTimeOffset.UtcNow);

    private static ReceiptPreview Receipt() => new(
        "Store", "Terminal", "Order", "Cashier", "Register", DateTimeOffset.UtcNow,
        DateOnly.FromDateTime(DateTime.Today), [], [], 12000m, 0m, 12000m, "receipt");

    private sealed class OrderingLifecycle(
        CheckoutSession checkout,
        ReceiptPreviewState receipt,
        ICurrentSessionContext session) : ISessionWorkflowLifecycle
    {
        public List<string> Calls { get; } = [];
        public List<bool> ReceiptWasCleared { get; } = [];
        public List<bool> CheckoutWasPresent { get; } = [];
        public List<bool> SessionWasPresent { get; } = [];

        public void CancelPayment() => Record("payment");
        public void ResetReceiptWorkflow() => Record("receipt");
        public void CloseSimulator() => Record("simulator");
        public void CloseCustomerDisplay() => Record("display");
        public void StopScanner() => Record("scanner");

        private void Record(string call)
        {
            Calls.Add(call);
            ReceiptWasCleared.Add(!receipt.HasReceipt);
            CheckoutWasPresent.Add(!checkout.Snapshot.IsEmpty);
            SessionWasPresent.Add(session.IsSignedIn);
        }
    }

    private sealed class NoopPaymentCoordinator : ICheckoutPaymentCoordinator
    {
        public Task<CheckoutPaymentExecutionResult> ExecuteAsync(
            RetailPOS.Domain.Payments.PaymentMethod method,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public void CancelActivePayment()
        {
        }
    }

    private sealed class SecondaryDisplayProvider : IDisplayTargetProvider
    {
        public IReadOnlyList<DisplayTarget> GetTargets() =>
            [new("secondary", "Secondary", new Rectangle(100, 0, 100, 90), false)];
    }

    private sealed class TrackingDisplayWindow : ICustomerDisplayWindow
    {
        public bool IsVisible { get; private set; }
        public bool WasClosed { get; private set; }
        public event EventHandler? Closed;
        public void ShowOn(DisplayTarget target) => IsVisible = true;
        public void MoveTo(DisplayTarget target) { }
        public void Activate() { }
        public void Close()
        {
            IsVisible = false;
            WasClosed = true;
            Closed?.Invoke(this, EventArgs.Empty);
        }
    }

    private sealed class ImmediateDispatcher : IUiDispatcher
    {
        public bool CheckAccess() => true;
        public Task InvokeAsync(Func<Task> action) => action();
    }

    private sealed class EmptyReceiptHistoryQuery : IReceiptHistoryQuery
    {
        public Task<ReceiptHistoryPage> SearchAsync(
            ReceiptHistoryRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new ReceiptHistoryPage([], HasMore: false));

        public Task<ReceiptPreview?> GetDetailAsync(
            Guid localOrderId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<ReceiptPreview?>(null);
    }

    private sealed class NoopReceiptPrinter : IReceiptPrinter
    {
        public Task<ReceiptPrintResult> PrintAsync(
            ReceiptPreview receipt,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class StubClock : ICheckoutClock
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
    }

    private sealed class StubProductRepository(Product product) : IProductRepository
    {
        public Task<IReadOnlyList<Product>> GetActiveAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Product>>([product]);
        public Task<IReadOnlyList<Product>> SearchAsync(string keyword, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Product>>([product]);
        public Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<Product?>(product.Id == id ? product : null);
        public Task<Product?> GetByBarcodeAsync(string barcode, CancellationToken cancellationToken = default) =>
            Task.FromResult<Product?>(product.Barcode == barcode ? product : null);
    }
}
