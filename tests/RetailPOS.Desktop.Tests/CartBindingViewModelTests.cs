using RetailPOS.Application.Checkout;
using RetailPOS.Application.Persistence;
using RetailPOS.Desktop.Workflow;
using RetailPOS.Desktop.ViewModels;
using RetailPOS.Domain.Payments;
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

        productViewModel.SelectedProduct = product;
        productViewModel.AddSelectedProductCommand.Execute(null);
        productViewModel.AddSelectedProductCommand.Execute(null);

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
    public void PaymentCommands_AreDisabledUntilCartHasPositiveTotal()
    {
        var session = new CheckoutSession();
        var viewModel = new CartPanelViewModel(session);

        Assert.False(viewModel.CanCheckout);
        Assert.False(viewModel.StartCardPaymentCommand.CanExecute(null));
        Assert.False(viewModel.OpenCashTenderCommand.CanExecute(null));

        session.AddProduct(Product("Water", 1000m));

        Assert.True(viewModel.CanCheckout);
        Assert.True(viewModel.StartCardPaymentCommand.CanExecute(null));
        Assert.True(viewModel.OpenCashTenderCommand.CanExecute(null));

        viewModel.DiscountInput = "5000";
        viewModel.ApplyFixedDiscountCommand.Execute(null);

        Assert.Equal(0m, viewModel.Total);
        Assert.False(viewModel.CanCheckout);
        Assert.False(viewModel.StartCardPaymentCommand.CanExecute(null));
        Assert.False(viewModel.OpenCashTenderCommand.CanExecute(null));
    }

    [Fact]
    public async Task CardPayment_IsInlineAndRequiresExplicitCompletionAfterApproval()
    {
        var session = new CheckoutSession();
        var coordinator = new RecordingPaymentCoordinator(session);
        var viewModel = new CartPanelViewModel(session, coordinator);
        var completionCount = 0;
        viewModel.CardPaymentCompleted += (_, _) => completionCount++;
        session.AddProduct(Product("Water", 1000m));

        await viewModel.StartCardPaymentCommand.ExecuteAsync(null);

        Assert.True(viewModel.IsCardPaymentVisible);
        Assert.True(viewModel.IsCardApproved);
        Assert.Equal(1000m, viewModel.CardAmountDue);
        Assert.Equal(PaymentMethod.Card, coordinator.Method);
        Assert.Equal(0, completionCount);
        Assert.True(viewModel.CompleteCardPaymentCommand.CanExecute(null));

        viewModel.CompleteCardPaymentCommand.Execute(null);

        Assert.Equal(1, completionCount);
        Assert.False(viewModel.IsCardPaymentVisible);
    }

    [Fact]
    public void DiscountMode_UsesOneApplyCommandAndSupportsQuickSelectAndReset()
    {
        var session = new CheckoutSession();
        var viewModel = new CartPanelViewModel(session);
        session.AddProduct(Product("Water", 10000m));

        viewModel.SelectPercentageDiscountCommand.Execute(null);
        viewModel.SelectQuickDiscountCommand.Execute("10");
        viewModel.ApplySelectedDiscountCommand.Execute(null);

        Assert.True(viewModel.IsPercentageDiscountMode);
        Assert.Equal(1000m, viewModel.DiscountAmount);

        viewModel.ResetDiscountCommand.Execute(null);

        Assert.Equal("0", viewModel.DiscountInput);
        Assert.Equal(0m, viewModel.DiscountAmount);
    }

    [Fact]
    public void DiscountInput_DefaultsToZero()
    {
        var viewModel = new CartPanelViewModel(new CheckoutSession());

        Assert.Equal("0", viewModel.DiscountInput);
    }

    [Theory]
    [InlineData("1000", 0)]
    [InlineData("1500", 500)]
    public void CashTender_CalculatesChangeAndBlocksUnderpayment(string received, decimal expectedChange)
    {
        var session = new CheckoutSession();
        session.AddProduct(Product("Water", 1000m));
        var viewModel = new CartPanelViewModel(session);
        viewModel.OpenCashTenderCommand.Execute(null);

        viewModel.CashReceivedInput = received;

        Assert.Equal(expectedChange, viewModel.ChangeDue);
        Assert.Equal(received == "1000" || received == "1500", viewModel.CompleteCashPaymentCommand.CanExecute(null));

        viewModel.CashReceivedInput = "999";
        Assert.False(viewModel.CompleteCashPaymentCommand.CanExecute(null));
        Assert.True(viewModel.HasCashTenderError);
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("1.5")]
    [InlineData("not-cash")]
    [InlineData("999999999999999999999999999999999999999999")]
    public void CashTender_RejectsInvalidInput(string input)
    {
        var session = new CheckoutSession();
        session.AddProduct(Product("Water", 1000m));
        var viewModel = new CartPanelViewModel(session);
        viewModel.OpenCashTenderCommand.Execute(null);

        viewModel.CashReceivedInput = input;

        Assert.True(viewModel.HasCashTenderError);
        Assert.False(viewModel.CompleteCashPaymentCommand.CanExecute(null));
    }

    [Fact]
    public void CashTender_KeypadQuickTenderAndCancelPreserveSale()
    {
        var session = new CheckoutSession();
        session.AddProduct(Product("Water", 3600m));
        var viewModel = new CartPanelViewModel(session);
        viewModel.OpenCashTenderCommand.Execute(null);

        Assert.Contains(viewModel.QuickTenderOptions, option => option.Amount == 4000m);
        viewModel.AppendCashDigitCommand.Execute("5");
        viewModel.AppendCashDigitCommand.Execute("00");
        Assert.Equal("500", viewModel.CashReceivedInput);
        viewModel.SelectQuickTenderCommand.Execute(4000m);
        Assert.Equal(400m, viewModel.ChangeDue);

        viewModel.CancelCashTenderCommand.Execute(null);

        Assert.False(viewModel.IsCashTenderVisible);
        Assert.Equal(3600m, viewModel.Total);
        Assert.Single(viewModel.Lines);
    }

    [Fact]
    public async Task CompleteCashPayment_RunsOnceAndRaisesCompletion()
    {
        var session = new CheckoutSession();
        session.AddProduct(Product("Water", 1000m));
        var coordinator = new RecordingPaymentCoordinator(session);
        var viewModel = new CartPanelViewModel(session, coordinator);
        var completions = 0;
        viewModel.CashPaymentCompleted += (_, _) => completions++;
        viewModel.OpenCashTenderCommand.Execute(null);
        viewModel.CashReceivedInput = "2000";

        await viewModel.CompleteCashPaymentCommand.ExecuteAsync(null);
        await viewModel.CompleteCashPaymentCommand.ExecuteAsync(null);

        Assert.Equal(1, coordinator.ExecutionCount);
        Assert.Equal(PaymentMethod.Cash, coordinator.Method);
        Assert.Equal(1, completions);
        Assert.Equal(0m, session.Snapshot.Total);
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

    private sealed class RecordingPaymentCoordinator(CheckoutSession session)
        : ICheckoutPaymentCoordinator
    {
        public int ExecutionCount { get; private set; }
        public PaymentMethod? Method { get; private set; }

        public Task<CheckoutPaymentExecutionResult> ExecuteAsync(
            PaymentMethod method,
            CancellationToken cancellationToken = default)
        {
            ExecutionCount++;
            Method = method;
            var amount = session.Snapshot.Total;
            session.Clear();
            var payment = new RecoverablePaymentStartResult(
                Guid.NewGuid(),
                Guid.NewGuid(),
                PendingCheckoutStatus.Completed,
                PaymentStatus.Approved,
                method,
                amount,
                amount,
                "APP-CASH",
                "CASH-TX",
                DateTimeOffset.UtcNow,
                null);
            return Task.FromResult(new CheckoutPaymentExecutionResult(payment, $"{method} payment approved."));
        }

        public void CancelActivePayment()
        {
        }
    }
}
