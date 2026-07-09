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

    [Fact]
    public void FixedDiscount_UpdatesSummaryAndCanBeCleared()
    {
        var session = new CheckoutSession();
        var viewModel = new CartPanelViewModel(session);
        session.AddProduct(Product("Water", 2000m));
        viewModel.DiscountInput = "500";

        viewModel.ApplyFixedDiscountCommand.Execute(null);

        Assert.Equal(500m, viewModel.DiscountAmount);
        Assert.Equal(1500m, viewModel.Total);
        Assert.Equal("Fixed 500 KRW", viewModel.DiscountDescription);
        Assert.True(viewModel.ClearDiscountCommand.CanExecute(null));

        viewModel.ClearDiscountCommand.Execute(null);

        Assert.Equal(0m, viewModel.DiscountAmount);
        Assert.Equal(2000m, viewModel.Total);
        Assert.False(viewModel.HasDiscount);
    }

    [Fact]
    public void PercentageDiscount_UsesDomainCalculation()
    {
        var session = new CheckoutSession();
        var viewModel = new CartPanelViewModel(session);
        session.AddProduct(Product("Water", 1999m));
        viewModel.DiscountInput = "10";

        viewModel.ApplyPercentageDiscountCommand.Execute(null);

        Assert.Equal(200m, viewModel.DiscountAmount);
        Assert.Equal(1799m, viewModel.Total);
        Assert.True(viewModel.HasDiscount);
    }

    [Theory]
    [InlineData("not-a-number", false)]
    [InlineData("101", true)]
    public void InvalidDiscountInput_IsRejected(string input, bool percentage)
    {
        var session = new CheckoutSession();
        var viewModel = new CartPanelViewModel(session);
        session.AddProduct(Product("Water", 1000m));
        viewModel.DiscountInput = input;

        if (percentage)
        {
            viewModel.ApplyPercentageDiscountCommand.Execute(null);
        }
        else
        {
            viewModel.ApplyFixedDiscountCommand.Execute(null);
        }

        Assert.True(viewModel.HasDiscountError);
        Assert.Equal(1000m, viewModel.Total);
        Assert.False(viewModel.HasDiscount);
    }

    [Fact]
    public void FixedDiscount_CannotReduceTotalBelowZero()
    {
        var session = new CheckoutSession();
        var viewModel = new CartPanelViewModel(session);
        session.AddProduct(Product("Water", 1000m));
        viewModel.DiscountInput = "5000";

        viewModel.ApplyFixedDiscountCommand.Execute(null);

        Assert.Equal(1000m, viewModel.DiscountAmount);
        Assert.Equal(0m, viewModel.Total);
    }

    [Fact]
    public void CheckoutCommand_IsDisabledUntilCartHasPositiveTotal()
    {
        var session = new CheckoutSession();
        var viewModel = new CartPanelViewModel(session);

        Assert.False(viewModel.CanCheckout);
        Assert.False(viewModel.CheckoutCommand.CanExecute(null));

        session.AddProduct(Product("Water", 1000m));

        Assert.True(viewModel.CanCheckout);
        Assert.True(viewModel.CheckoutCommand.CanExecute(null));

        viewModel.DiscountInput = "5000";
        viewModel.ApplyFixedDiscountCommand.Execute(null);

        Assert.Equal(0m, viewModel.Total);
        Assert.False(viewModel.CanCheckout);
        Assert.False(viewModel.CheckoutCommand.CanExecute(null));
    }

    [Fact]
    public void CheckoutCommand_RaisesCheckoutRequestedForPayableCart()
    {
        var session = new CheckoutSession();
        var viewModel = new CartPanelViewModel(session);
        var requestCount = 0;
        viewModel.CheckoutRequested += (_, _) => requestCount++;
        session.AddProduct(Product("Water", 1000m));

        viewModel.CheckoutCommand.Execute(null);

        Assert.Equal(1, requestCount);
    }

    [Fact]
    public void Dispose_UnsubscribesFromCheckoutSessionChanges()
    {
        var session = new CheckoutSession();
        var viewModel = new CartPanelViewModel(session);

        viewModel.Dispose();
        session.AddProduct(Product("Water", 1000m));

        Assert.Equal(0, viewModel.ItemCount);
        Assert.Empty(viewModel.Lines);
        Assert.Equal(0m, viewModel.Total);
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
