using Microsoft.Extensions.Logging.Abstractions;
using RetailPOS.Application.Checkout;
using RetailPOS.Application.Persistence;
using RetailPOS.Desktop.DeviceSimulation;
using RetailPOS.Desktop.ViewModels;
using RetailPOS.Domain.Products;
using RetailPOS.Infrastructure.Devices;

namespace RetailPOS.Desktop.Tests;

public sealed class BarcodeScannerCoordinatorTests
{
    [Fact]
    public async Task BackgroundScanUsesDispatcherAndSharedLookupWithoutChangingBarcodeText()
    {
        var product = Product("Cola", "8801000000011");
        var session = new CheckoutSession();
        var viewModel = new ProductGridViewModel(
            new StubProductRepository(product),
            session)
        {
            BarcodeText = "manual-entry-stays"
        };
        using var scanner = new SimulatedBarcodeScanner
        {
            EmitOnBackgroundThread = true
        };
        var dispatcher = new RecordingDispatcher();
        using var coordinator = new BarcodeScannerCoordinator(
            scanner,
            viewModel,
            dispatcher,
            NullLogger<BarcodeScannerCoordinator>.Instance);
        coordinator.Start();

        await scanner.EmitAsync(product.Barcode);
        await WaitUntilAsync(() => session.Snapshot.Lines.Count == 1);

        Assert.Equal(1, dispatcher.InvocationCount);
        Assert.Equal("manual-entry-stays", viewModel.BarcodeText);
        Assert.Equal(product.Id, Assert.Single(session.Snapshot.Lines).ProductId);
    }

    [Fact]
    public async Task UnknownScanShowsSafeMessageAndDoesNotChangeCart()
    {
        var session = new CheckoutSession();
        using var scanner = new SimulatedBarcodeScanner();
        var viewModel = new ProductGridViewModel(new StubProductRepository(null), session);
        using var coordinator = new BarcodeScannerCoordinator(
            scanner,
            viewModel,
            new RecordingDispatcher(),
            NullLogger<BarcodeScannerCoordinator>.Instance);
        coordinator.Start();

        await scanner.EmitAsync("missing");
        await WaitUntilAsync(() => viewModel.HasBarcodeMessage);

        Assert.Empty(session.Snapshot.Lines);
        Assert.Contains("not found", viewModel.BarcodeMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task StopAndRestartDoNotCreateDuplicateSubscriptions()
    {
        var product = Product("Water", "known");
        var session = new CheckoutSession();
        using var scanner = new SimulatedBarcodeScanner();
        var viewModel = new ProductGridViewModel(new StubProductRepository(product), session);
        using var coordinator = new BarcodeScannerCoordinator(
            scanner,
            viewModel,
            new RecordingDispatcher(),
            NullLogger<BarcodeScannerCoordinator>.Instance);

        coordinator.Start();
        coordinator.Start();
        coordinator.Stop();
        await scanner.EmitAsync("known");
        Assert.Empty(session.Snapshot.Lines);

        coordinator.Start();
        await scanner.EmitAsync("known");
        await WaitUntilAsync(() => session.Snapshot.Lines.Count == 1);

        Assert.Equal(1, Assert.Single(session.Snapshot.Lines).Quantity);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        while (!condition())
        {
            await Task.Delay(10, timeout.Token);
        }
    }

    private static Product Product(string name, string barcode) => new(
        Guid.NewGuid(),
        $"SKU-{name}",
        barcode,
        name,
        "Beverages",
        1000m);

    private sealed class RecordingDispatcher : IUiDispatcher
    {
        public int InvocationCount { get; private set; }
        public bool CheckAccess() => false;

        public async Task InvokeAsync(Func<Task> action)
        {
            InvocationCount++;
            await action();
        }
    }

    private sealed class StubProductRepository(Product? product) : IProductRepository
    {
        public Task<IReadOnlyList<Product>> GetActiveAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Product>>([]);

        public Task<IReadOnlyList<Product>> SearchAsync(
            string keyword,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Product>>([]);

        public Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<Product?>(null);

        public Task<Product?> GetByBarcodeAsync(
            string barcode,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(product?.Barcode == barcode ? product : null);
    }
}
