using RetailPOS.Application.Checkout;
using RetailPOS.Application.Persistence;
using RetailPOS.Desktop.ViewModels;

namespace RetailPOS.Desktop.Tests;

public sealed class CheckoutRecoveryViewModelTests
{
    private static readonly Guid PendingCheckoutId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");

    [Fact]
    public async Task LoadAsync_PopulatesRecoverableItems()
    {
        var service = new RecordingCheckoutRecoveryService(Record());
        var viewModel = new CheckoutRecoveryViewModel(service);

        await viewModel.LoadAsync();

        var item = Assert.Single(viewModel.Items);
        Assert.Equal(PendingCheckoutId, item.PendingCheckoutId);
        Assert.Equal(item, viewModel.SelectedItem);
        Assert.True(viewModel.HasItems);
        Assert.True(viewModel.CompleteOrderCommand.CanExecute(null));
    }

    [Fact]
    public async Task CompleteOrderCommand_CompletesSelectedItemAndReloads()
    {
        var service = new RecordingCheckoutRecoveryService(Record());
        var viewModel = new CheckoutRecoveryViewModel(service);
        await viewModel.LoadAsync();

        await viewModel.CompleteOrderCommand.ExecuteAsync(null);

        Assert.Equal(PendingCheckoutId, service.CompletedPendingCheckoutId);
        Assert.Empty(viewModel.Items);
        Assert.False(viewModel.HasItems);
    }

    [Fact]
    public async Task RequestManagerReviewCommand_MarksSelectedItemAndReloads()
    {
        var service = new RecordingCheckoutRecoveryService(Record());
        var viewModel = new CheckoutRecoveryViewModel(service);
        await viewModel.LoadAsync();

        await viewModel.RequestManagerReviewCommand.ExecuteAsync(null);

        Assert.Equal(PendingCheckoutId, service.ManagerReviewPendingCheckoutId);
        Assert.Empty(viewModel.Items);
    }

    [Fact]
    public async Task CompleteOrderCommand_ShowsUserSafeFailureMessage()
    {
        var service = new RecordingCheckoutRecoveryService(
            Record(),
            completeResult: new CheckoutRecoveryCompletionResult(
                false,
                null,
                false,
                "Recovery could not complete automatically. Request manager review before returning to checkout."));
        var viewModel = new CheckoutRecoveryViewModel(service);
        await viewModel.LoadAsync();

        await viewModel.CompleteOrderCommand.ExecuteAsync(null);

        Assert.Contains("manager review", viewModel.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Single(viewModel.Items);
    }

    [Fact]
    public async Task ManagerReviewItem_CannotCompleteOrderOrRequestReviewAgain()
    {
        var service = new RecordingCheckoutRecoveryService(Record() with
        {
            RecoveryStatus = PendingCheckoutStatus.ManagerReviewRequired,
            CanCompleteOrder = false
        });
        var viewModel = new CheckoutRecoveryViewModel(service);

        await viewModel.LoadAsync();

        Assert.False(viewModel.CompleteOrderCommand.CanExecute(null));
        Assert.False(viewModel.RequestManagerReviewCommand.CanExecute(null));
        Assert.Equal("REVIEW", viewModel.SelectedItem?.StatusLabel);
    }

    private static CheckoutRecoveryRecord Record() => new(
        PendingCheckoutId,
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid(),
        new DateTimeOffset(2026, 7, 8, 1, 0, 0, TimeSpan.Zero),
        PendingCheckoutStatus.ApprovedButOrderNotCreated,
        3600m,
        "Approved",
        "APP-001",
        "TX-001",
        new DateTimeOffset(2026, 7, 8, 1, 0, 5, TimeSpan.Zero),
        Guid.NewGuid(),
        [new CheckoutRecoveryLine("Cola", 2, 1800m, 3600m)],
        3600m,
        0m,
        3600m,
        true,
        true,
        null);

    private sealed class RecordingCheckoutRecoveryService(
        CheckoutRecoveryRecord record,
        CheckoutRecoveryCompletionResult? completeResult = null) : ICheckoutRecoveryService
    {
        private readonly List<CheckoutRecoveryRecord> _records = [record];

        public Guid? CompletedPendingCheckoutId { get; private set; }
        public Guid? ManagerReviewPendingCheckoutId { get; private set; }

        public Task<IReadOnlyList<CheckoutRecoveryRecord>> GetRecoverableAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<CheckoutRecoveryRecord>>(_records.ToList());

        public Task<CheckoutRecoveryCompletionResult> CompleteAsync(
            Guid pendingCheckoutId,
            CancellationToken cancellationToken = default)
        {
            CompletedPendingCheckoutId = pendingCheckoutId;
            var result = completeResult ?? new CheckoutRecoveryCompletionResult(
                true,
                Guid.NewGuid(),
                false,
                "Order recovery completed.");
            if (result.Succeeded)
            {
                _records.RemoveAll(item => item.PendingCheckoutId == pendingCheckoutId);
            }

            return Task.FromResult(result);
        }

        public Task MarkManagerReviewRequiredAsync(
            Guid pendingCheckoutId,
            CancellationToken cancellationToken = default)
        {
            ManagerReviewPendingCheckoutId = pendingCheckoutId;
            _records.RemoveAll(item => item.PendingCheckoutId == pendingCheckoutId);
            return Task.CompletedTask;
        }
    }
}
