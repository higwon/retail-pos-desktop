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
            checkoutSession)
        {
            BarcodeText = "8801000000011"
        };

        await productGrid.ScanBarcodeCommand.ExecuteAsync(null);
        productGrid.SearchText = "Sunscreen";
        await productGrid.SearchCommand.ExecuteAsync(null);
        productGrid.AddProductCommand.Execute(Assert.Single(productGrid.Products));

        Assert.Equal(2, checkoutSession.Snapshot.ItemCount);
        Assert.Equal(30000m, checkoutSession.Snapshot.Subtotal);

        using var cartPanel = new CartPanelViewModel(checkoutSession)
        {
            DiscountInput = "3000"
        };
        var checkoutRequested = false;
        cartPanel.CheckoutRequested += (_, _) => checkoutRequested = true;

        cartPanel.ApplyFixedDiscountCommand.Execute(null);
        cartPanel.CheckoutCommand.Execute(null);

        Assert.True(checkoutRequested);
        Assert.Equal(3000m, cartPanel.DiscountAmount);
        Assert.Equal(27000m, cartPanel.Total);

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
            new DemoReceiptContextProvider(),
            clock);
        using var payment = new PaymentDialogViewModel(
            checkoutSession,
            paymentStart,
            orderCompletion,
            receiptService,
            receiptState,
            new CheckoutDisplayState());

        var paymentExecution = payment.ApproveCardPaymentCommand.ExecuteAsync(null);
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

        Assert.True(payment.IsApproved);
        Assert.Equal(PaymentMethod.Card, payment.Method);
        Assert.Equal(27000m, payment.ApprovedAmount);
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

    private sealed class StubTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class StubCheckoutClock(DateTimeOffset utcNow) : ICheckoutClock
    {
        public DateTimeOffset UtcNow => utcNow;
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
