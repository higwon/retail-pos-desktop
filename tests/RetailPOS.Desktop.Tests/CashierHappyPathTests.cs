using System.IO;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RetailPOS.Application.Authentication;
using RetailPOS.Application.Checkout;
using RetailPOS.Application.Orders;
using RetailPOS.Application.Payments;
using RetailPOS.Application.Persistence;
using RetailPOS.Application.Receipts;
using RetailPOS.Desktop.ViewModels;
using RetailPOS.Desktop.Workflow;
using RetailPOS.Domain.Payments;
using RetailPOS.Infrastructure.DependencyInjection;
using RetailPOS.Infrastructure.Devices;
using RetailPOS.Infrastructure.Persistence;

namespace RetailPOS.Desktop.Tests;

public sealed class CashierHappyPathTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 7, 10, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid CheckoutId =
        Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid OrderId =
        Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001");
    private static readonly Guid QueueItemId =
        Guid.Parse("cccccccc-0000-0000-0000-000000000001");

    [Fact]
    public async Task DemoCashierFlow_CreatesReceiptAndPendingSyncQueue()
    {
        await using var harness = await PersistenceHarness.CreateAsync();
        var services = harness.Services;
        var sessionContext = new CurrentSessionContext();
        var timeProvider = new StubTimeProvider(Now);
        var login = new LoginViewModel(new DemoLoginService(sessionContext, timeProvider))
        {
            EmployeeCode = "E0001",
            Password = "1234"
        };
        var signedIn = false;
        login.SignedIn += (_, _) => signedIn = true;

        await login.SignInCommand.ExecuteAsync(null);

        Assert.True(signedIn);
        Assert.True(sessionContext.IsSignedIn);
        Assert.Equal("E0001", sessionContext.Current!.EmployeeCode);

        var checkoutSession = new CheckoutSession();
        var productGrid = new ProductGridViewModel(
            services.GetRequiredService<IProductRepository>(),
            checkoutSession);

        await productGrid.ProcessBarcodeAsync("8801000000011");
        productGrid.SearchText = "Sunscreen";
        await productGrid.SearchCommand.ExecuteAsync(null);
        productGrid.SelectedProduct = Assert.Single(productGrid.Products);
        productGrid.AddSelectedProductCommand.Execute(null);

        Assert.Equal(2, checkoutSession.Snapshot.ItemCount);
        Assert.Equal(30000m, checkoutSession.Snapshot.Subtotal);

        var clock = new StubCheckoutClock(Now);
        var idGenerator = new SequenceCheckoutIdGenerator(
            CheckoutId,
            OrderId,
            QueueItemId);
        var pendingRepository = services.GetRequiredService<IPendingCheckoutRepository>();
        var orderRepository = services.GetRequiredService<IOrderRepository>();
        var syncQueueRepository = services.GetRequiredService<ISyncQueueRepository>();
        using var paymentTerminal = new SimulatedPaymentTerminal(timeProvider);
        var paymentStart = new RecoverablePaymentStartService(
            pendingRepository,
            paymentTerminal,
            new LocalCashPaymentProcessor(timeProvider),
            sessionContext,
            clock,
            idGenerator);
        var orderCompletion = new OrderCompletionService(
            pendingRepository,
            orderRepository,
            syncQueueRepository,
            services.GetRequiredService<ILocalTransaction>(),
            clock,
            idGenerator);
        var receiptState = new ReceiptPreviewState();
        var receiptService = new ReceiptService(
            orderRepository,
            new DemoReceiptContextProvider());
        var paymentCoordinator = new CheckoutPaymentCoordinator(
            checkoutSession,
            paymentStart,
            orderCompletion,
            receiptService,
            receiptState,
            new CheckoutDisplayState());
        using var cartPanel = new CartPanelViewModel(checkoutSession, paymentCoordinator)
        {
            DiscountInput = "3000"
        };
        cartPanel.ApplySelectedDiscountCommand.Execute(null);

        Assert.Equal(3000m, cartPanel.DiscountAmount);
        Assert.Equal(27000m, cartPanel.Total);

        var paymentExecution = cartPanel.StartCardPaymentCommand.ExecuteAsync(null);
        for (var attempt = 0; paymentTerminal.PendingRequest is null && attempt < 100; attempt++)
        {
            await Task.Delay(10);
        }
        var terminalRequest = Assert.IsType<DeviceRequest<PaymentTerminalRequestPayload, PaymentTerminalResponse>>(
            paymentTerminal.PendingRequest);
        paymentTerminal.Respond(terminalRequest.RequestId, new(
            PaymentTerminalResponseOutcome.Approve,
            "APP-HAPPY-PATH",
            $"TX-{terminalRequest.Payload.PaymentAttemptId:N}"));
        await paymentExecution;

        Assert.True(cartPanel.IsCardApproved);
        Assert.Equal("APP-HAPPY-PATH", cartPanel.CardApprovalCode);
        Assert.True(checkoutSession.Snapshot.IsEmpty);

        var receipt = receiptState.Current;
        Assert.NotNull(receipt);
        var order = await orderRepository.GetByIdAsync(OrderId);
        Assert.NotNull(order);
        Assert.Equal(3000m, order!.DiscountAmount);
        Assert.Equal(27000m, order.TotalAmount);
        Assert.Equal(PaymentStatus.Approved, Assert.Single(order.Payments).Status);
        Assert.Equal(2, receipt!.Lines.Count);
        Assert.Equal(3000m, receipt.DiscountAmount);
        Assert.Equal(27000m, receipt.TotalAmount);
        Assert.Contains(receipt.OrderNumber, receipt.PlainText);

        var pending = await pendingRepository.GetByIdAsync(CheckoutId);
        Assert.NotNull(pending);
        Assert.Equal(PendingCheckoutStatus.Completed, pending!.RecoveryStatus);
        Assert.Equal(OrderId, pending.OrderId);

        var queueItem = Assert.Single(await syncQueueRepository.GetRecentAsync(10));
        Assert.Equal(QueueItemId, queueItem.Id);
        Assert.Equal("Order", queueItem.ItemType);
        Assert.Equal(OrderId, queueItem.AggregateId);
        Assert.Equal(SyncQueueStatus.Pending, queueItem.Status);
        var payload = JsonSerializer.Deserialize<OrderUploadPayload>(
            queueItem.PayloadJson!,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.NotNull(payload);
        Assert.Equal(3000m, payload!.DiscountAmount);
        Assert.Equal(27000m, payload.TotalAmount);
    }

    [Fact]
    public async Task DemoCashFlow_PersistsTenderAndLoadsReceiptHistory()
    {
        await using var harness = await PersistenceHarness.CreateAsync();
        var services = harness.Services;
        var sessionContext = new CurrentSessionContext();
        var timeProvider = new StubTimeProvider(Now);
        var login = new LoginViewModel(new DemoLoginService(sessionContext, timeProvider))
        {
            EmployeeCode = "E0001",
            Password = "1234"
        };
        await login.SignInCommand.ExecuteAsync(null);

        var checkoutSession = new CheckoutSession();
        var productGrid = new ProductGridViewModel(
            services.GetRequiredService<IProductRepository>(),
            checkoutSession);
        await productGrid.ProcessBarcodeAsync("8801000000011");

        var clock = new StubCheckoutClock(Now);
        var idGenerator = new SequenceCheckoutIdGenerator(
            CheckoutId,
            OrderId,
            QueueItemId);
        var pendingRepository = services.GetRequiredService<IPendingCheckoutRepository>();
        var orderRepository = services.GetRequiredService<IOrderRepository>();
        var receiptHistoryRepository = services.GetRequiredService<IReceiptHistoryRepository>();
        var receiptContext = new DemoReceiptContextProvider();
        var receiptService = new ReceiptService(orderRepository, receiptContext);
        var paymentStart = new RecoverablePaymentStartService(
            pendingRepository,
            new UnusedPaymentTerminal(),
            new LocalCashPaymentProcessor(timeProvider),
            sessionContext,
            clock,
            idGenerator);
        var orderCompletion = new OrderCompletionService(
            pendingRepository,
            orderRepository,
            services.GetRequiredService<ISyncQueueRepository>(),
            services.GetRequiredService<ILocalTransaction>(),
            clock,
            idGenerator);
        var receiptState = new ReceiptPreviewState();
        var paymentCoordinator = new CheckoutPaymentCoordinator(
            checkoutSession,
            paymentStart,
            orderCompletion,
            receiptService,
            receiptState,
            new CheckoutDisplayState());
        using var cartPanel = new CartPanelViewModel(checkoutSession, paymentCoordinator);
        var completionCount = 0;
        cartPanel.CashPaymentCompleted += (_, _) => completionCount++;

        cartPanel.OpenCashTenderCommand.Execute(null);
        cartPanel.CashReceivedInput = "20000";
        await cartPanel.CompleteCashPaymentCommand.ExecuteAsync(null);

        Assert.Equal(1, completionCount);
        Assert.True(checkoutSession.Snapshot.IsEmpty);
        var order = await orderRepository.GetByIdAsync(OrderId);
        Assert.NotNull(order);
        var payment = Assert.Single(order.Payments);
        Assert.Equal(PaymentMethod.Cash, payment.Method);
        Assert.Equal(20000m, payment.CashTenderedAmount);
        Assert.Equal(8000m, payment.ChangeAmount);

        var history = new ReceiptHistoryQuery(
            receiptHistoryRepository,
            receiptService,
            receiptContext);
        var page = await history.SearchAsync(new ReceiptHistoryRequest(
            DateOnly.FromDateTime(Now.LocalDateTime),
            order.LocalOrderNumber));
        var summary = Assert.Single(page.Items);
        var detail = await history.GetDetailAsync(summary.LocalOrderId);
        Assert.NotNull(detail);
        var receiptPayment = Assert.Single(detail.Payments);
        Assert.Equal(PaymentMethod.Cash, receiptPayment.Method);
        Assert.Equal(20000m, receiptPayment.CashTenderedAmount);
        Assert.Equal(8000m, receiptPayment.ChangeAmount);
    }

    private sealed class StubTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class StubCheckoutClock(DateTimeOffset utcNow) : ICheckoutClock
    {
        public DateTimeOffset UtcNow => utcNow;
    }

    private sealed class UnusedPaymentTerminal : IPaymentTerminal
    {
        public Task<PaymentAuthorizationResult> AuthorizeAsync(
            PaymentAuthorizationRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("The cash flow must not call the card terminal.");
    }

    private sealed class SequenceCheckoutIdGenerator(params Guid[] ids) : ICheckoutIdGenerator
    {
        private readonly Queue<Guid> _ids = new(ids);

        public Guid NewId() => _ids.Dequeue();
    }

    private sealed class PersistenceHarness : IAsyncDisposable
    {
        private readonly ServiceProvider _provider;
        private readonly AsyncServiceScope _scope;
        private readonly string _directory;

        private PersistenceHarness(
            ServiceProvider provider,
            AsyncServiceScope scope,
            string directory)
        {
            _provider = provider;
            _scope = scope;
            _directory = directory;
        }

        public IServiceProvider Services => _scope.ServiceProvider;

        public static async Task<PersistenceHarness> CreateAsync()
        {
            var directory = Path.Combine(
                Path.GetTempPath(),
                "RetailPOS.Tests",
                Guid.NewGuid().ToString("N"));
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["LocalDatabase:DatabasePath"] = Path.Combine(directory, "retail-pos.db")
                })
                .Build();
            var services = new ServiceCollection();
            services.AddLocalPersistence(configuration);
            var provider = services.BuildServiceProvider(
                new ServiceProviderOptions { ValidateScopes = true });
            var scope = provider.CreateAsyncScope();
            var harness = new PersistenceHarness(provider, scope, directory);
            await harness.Services
                .GetRequiredService<LocalDatabaseInitializer>()
                .InitializeAsync();
            return harness;
        }

        public async ValueTask DisposeAsync()
        {
            await _scope.DisposeAsync();
            await _provider.DisposeAsync();
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, recursive: true);
            }
        }
    }
}
