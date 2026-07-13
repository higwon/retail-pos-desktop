using System.Drawing;
using Microsoft.Extensions.Logging.Abstractions;
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

        var pendingPayment = new PendingOperation();
        var receiptOperation = new PendingOperation();
        using var paymentHost = new WorkflowWindowHost<TestWorkflowWindow>(
            () => new TestWorkflowWindow(pendingPayment));
        using var receiptHost = new WorkflowWindowHost<TestWorkflowWindow>(
            () => new TestWorkflowWindow(receiptOperation));
        paymentHost.ShowOrActivate();
        receiptHost.ShowOrActivate();
        var workflow = new SessionWorkflowLifecycle(
            scannerCoordinator,
            new SessionWorkflowWindows(paymentHost, receiptHost),
            displayHost);
        var navigator = new CashierWorkflowNavigator();
        navigator.Reset(CashierWorkflowScreen.Register);
        var coordinator = new SessionSignOutCoordinator(
            workflow,
            checkout,
            receiptState,
            session,
            navigator);

        coordinator.SignOut();

        Assert.True(pendingPayment.IsCancellationRequested);
        Assert.True(receiptOperation.IsCancellationRequested);
        Assert.False(paymentHost.IsOpen);
        Assert.False(receiptHost.IsOpen);
        Assert.False(displayHost.IsOpen);
        Assert.True(displayWindow.WasClosed);
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
        var navigator = new CashierWorkflowNavigator();
        navigator.Reset(CashierWorkflowScreen.Register);
        var coordinator = new SessionSignOutCoordinator(
            lifecycle,
            checkout,
            receiptState,
            session,
            navigator);

        coordinator.SignOut();

        Assert.Equal(
            ["payment", "receipt", "display", "scanner"],
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

    private static CashierSession Cashier(string employeeCode, string name) => new(
        Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), employeeCode, name, DateTimeOffset.UtcNow);

    private static ReceiptPreview Receipt() => new(
        "Store", "Terminal", "Order", "Cashier", "Register", DateTimeOffset.UtcNow,
        DateOnly.FromDateTime(DateTime.Today), [], [], 12000m, 0m, 12000m, "receipt");

    private sealed class PendingOperation : IDisposable
    {
        private readonly CancellationTokenSource _cancellation = new();
        public bool IsCancellationRequested => _cancellation.IsCancellationRequested;
        public void Dispose() => _cancellation.Cancel();
    }

    private sealed class TestWorkflowWindow(IDisposable viewModel) : IWorkflowWindow
    {
        public bool IsVisible { get; private set; }
        public event EventHandler? Closed;
        public void Show() => IsVisible = true;
        public void Activate() { }
        public void Close()
        {
            if (!IsVisible) return;
            IsVisible = false;
            viewModel.Dispose();
            Closed?.Invoke(this, EventArgs.Empty);
        }
    }

    private sealed class OrderingLifecycle(
        CheckoutSession checkout,
        ReceiptPreviewState receipt,
        ICurrentSessionContext session) : ISessionWorkflowLifecycle
    {
        public List<string> Calls { get; } = [];
        public List<bool> ReceiptWasCleared { get; } = [];
        public List<bool> CheckoutWasPresent { get; } = [];
        public List<bool> SessionWasPresent { get; } = [];

        public void ClosePayment() => Record("payment");
        public void CloseReceipt() => Record("receipt");
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
