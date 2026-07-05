using RetailPOS.Application.Checkout;
using RetailPOS.Application.Persistence;
using RetailPOS.Desktop.ViewModels;
using RetailPOS.Domain.Products;

namespace RetailPOS.Desktop.Tests;

public sealed class CartBindingViewModelTests
{
    [Fact]
    public async Task ProductAndCartViewModels_ShareCheckoutSessionState()
    {
        var product = Product("Cola", 1800m);
        var session = new CheckoutSession();
        var productViewModel = new ProductGridViewModel(new StubProductRepository(product), session);
        var cartViewModel = new CartPanelViewModel(session);
        await productViewModel.LoadAsync();

        productViewModel.AddProductCommand.Execute(product);
        productViewModel.AddProductCommand.Execute(product);

        var line = Assert.Single(cartViewModel.Lines);
        Assert.Equal(2, line.Quantity);
        Assert.Equal(3600m, cartViewModel.Total);
        Assert.Equal(2, cartViewModel.ItemCount);
        Assert.True(cartViewModel.HasItems);

        cartViewModel.DecreaseQuantityCommand.Execute(product.Id);
        Assert.Equal(1, Assert.Single(cartViewModel.Lines).Quantity);

        cartViewModel.RemoveProductCommand.Execute(product.Id);
        Assert.True(cartViewModel.IsEmpty);
        Assert.False(cartViewModel.ClearCommand.CanExecute(null));
    }

    [Fact]
    public void ClearCommand_ResetsAllCartState()
    {
        var product = Product("Water", 1000m);
        var session = new CheckoutSession();
        var viewModel = new CartPanelViewModel(session);
        session.AddProduct(product);

        viewModel.ClearCommand.Execute(null);

        Assert.Empty(viewModel.Lines);
        Assert.Equal(0m, viewModel.Subtotal);
        Assert.Equal(0m, viewModel.Total);
        Assert.Equal(0, viewModel.ItemCount);
    }

    private static Product Product(string name, decimal price) => new(
        Guid.NewGuid(), $"SKU-{name}", Guid.NewGuid().ToString("N"), name, "Beverages", price);

    private sealed class StubProductRepository(Product product) : IProductRepository
    {
        public Task<IReadOnlyList<Product>> GetActiveAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Product>>([product]);

        public Task<IReadOnlyList<Product>> SearchAsync(string keyword, CancellationToken cancellationToken = default) =>
            GetActiveAsync(cancellationToken);

        public Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<Product?>(null);

        public Task<Product?> GetByBarcodeAsync(string barcode, CancellationToken cancellationToken = default) =>
            Task.FromResult<Product?>(null);
    }
}
