using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RetailPOS.Application.Checkout;
using RetailPOS.Application.Persistence;

namespace RetailPOS.Desktop.ViewModels;

public sealed partial class CheckoutRecoveryViewModel : ObservableObject
{
    private readonly ICheckoutRecoveryService _checkoutRecoveryService;

    public CheckoutRecoveryViewModel(ICheckoutRecoveryService checkoutRecoveryService)
    {
        _checkoutRecoveryService = checkoutRecoveryService;
        Items.CollectionChanged += OnItemsChanged;
        LoadCommand = new AsyncRelayCommand(LoadAsync);
        CompleteOrderCommand = new AsyncRelayCommand(CompleteOrderAsync, CanCompleteSelectedItem);
        RequestManagerReviewCommand = new AsyncRelayCommand(
            RequestManagerReviewAsync,
            CanRequestManagerReview);
    }

    public ObservableCollection<CheckoutRecoveryItemViewModel> Items { get; } = [];
    public IAsyncRelayCommand LoadCommand { get; }
    public IAsyncRelayCommand CompleteOrderCommand { get; }
    public IAsyncRelayCommand RequestManagerReviewCommand { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedItem))]
    private CheckoutRecoveryItemViewModel? _selectedItem;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusMessage = "Checking for interrupted approved payments.";

    [ObservableProperty]
    private string? _errorMessage;

    public bool HasItems => Items.Count > 0;
    public bool HasSelectedItem => SelectedItem is not null;

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        IsBusy = true;
        ErrorMessage = null;

        try
        {
            var records = await _checkoutRecoveryService.GetRecoverableAsync(cancellationToken);
            Items.Clear();
            foreach (var item in records.Select(record => new CheckoutRecoveryItemViewModel(record)))
            {
                Items.Add(item);
            }

            SelectedItem = Items.FirstOrDefault();
            StatusMessage = Items.Count == 0
                ? "No checkout needs recovery or review."
                : $"{Items.Count:N0} checkout requires recovery or review.";
        }
        catch (Exception)
        {
            ErrorMessage = "Recovery records could not be loaded. Try again or ask a manager to review the terminal.";
        }
        finally
        {
            IsBusy = false;
            NotifyCommandStateChanged();
        }
    }

    private async Task CompleteOrderAsync()
    {
        if (SelectedItem is null)
        {
            return;
        }

        IsBusy = true;
        ErrorMessage = null;

        try
        {
            var result = await _checkoutRecoveryService.CompleteAsync(SelectedItem.PendingCheckoutId);
            if (result.Succeeded)
            {
                StatusMessage = result.Message;
                await LoadAsync();
            }
            else
            {
                ErrorMessage = result.Message;
            }
        }
        catch (Exception)
        {
            ErrorMessage = "Recovery could not complete automatically. Request manager review before returning to checkout.";
        }
        finally
        {
            IsBusy = false;
            NotifyCommandStateChanged();
        }
    }

    private async Task RequestManagerReviewAsync()
    {
        if (SelectedItem is null)
        {
            return;
        }

        IsBusy = true;
        ErrorMessage = null;

        try
        {
            await _checkoutRecoveryService.MarkManagerReviewRequiredAsync(SelectedItem.PendingCheckoutId);
            StatusMessage = "Checkout was sent to manager review.";
            await LoadAsync();
        }
        catch (Exception)
        {
            ErrorMessage = "Manager review could not be requested. Try again or ask a manager to check this terminal.";
        }
        finally
        {
            IsBusy = false;
            NotifyCommandStateChanged();
        }
    }

    partial void OnSelectedItemChanged(CheckoutRecoveryItemViewModel? value)
    {
        NotifyCommandStateChanged();
    }

    partial void OnIsBusyChanged(bool value)
    {
        NotifyCommandStateChanged();
    }

    private bool CanCompleteSelectedItem() =>
        SelectedItem?.CanCompleteOrder == true && !IsBusy;

    private bool CanRequestManagerReview() =>
        SelectedItem?.CanRequestManagerReview == true && !IsBusy;

    private void OnItemsChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(HasItems));
    }

    private void NotifyCommandStateChanged()
    {
        CompleteOrderCommand.NotifyCanExecuteChanged();
        RequestManagerReviewCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(HasSelectedItem));
    }
}

public sealed class CheckoutRecoveryItemViewModel
{
    public CheckoutRecoveryItemViewModel(CheckoutRecoveryRecord record)
    {
        PendingCheckoutId = record.PendingCheckoutId;
        StoreId = record.StoreId;
        TerminalId = record.TerminalId;
        CashierId = record.CashierId;
        OrderId = record.OrderId;
        CreatedAtLocal = record.CreatedAtUtc.ToLocalTime();
        RecoveryStatus = record.RecoveryStatus;
        PaymentApprovedAtLocal = record.PaymentApprovedAtUtc?.ToLocalTime();
        ApprovedAmount = record.ApprovedAmount;
        PaymentMethod = record.PaymentMethod;
        ApprovalCode = record.ApprovalCode ?? "-";
        TransactionReference = record.TransactionReference ?? "-";
        IsSnapshotReadable = record.IsSnapshotReadable;
        CanCompleteOrder = record.CanCompleteOrder;
        WarningMessage = record.WarningMessage;
        Lines = record.Lines.Select(line => new CheckoutRecoveryLineViewModel(line)).ToList();
        CartSubtotal = record.CartSubtotal;
        DiscountAmount = record.DiscountAmount;
        CartTotal = record.CartTotal;
    }

    public Guid PendingCheckoutId { get; }
    public Guid StoreId { get; }
    public Guid TerminalId { get; }
    public Guid CashierId { get; }
    public Guid? OrderId { get; }
    public DateTimeOffset CreatedAtLocal { get; }
    public PendingCheckoutStatus RecoveryStatus { get; }
    public DateTimeOffset? PaymentApprovedAtLocal { get; }
    public decimal ApprovedAmount { get; }
    public string PaymentMethod { get; }
    public string ApprovalCode { get; }
    public string TransactionReference { get; }
    public bool IsSnapshotReadable { get; }
    public bool CanCompleteOrder { get; }
    public bool CanRequestManagerReview =>
        RecoveryStatus != PendingCheckoutStatus.ManagerReviewRequired;
    public string? WarningMessage { get; }
    public IReadOnlyList<CheckoutRecoveryLineViewModel> Lines { get; }
    public decimal CartSubtotal { get; }
    public decimal DiscountAmount { get; }
    public decimal CartTotal { get; }

    public string CheckoutLabel => $"Checkout {PendingCheckoutId.ToString("N")[..8].ToUpperInvariant()}";
    public string StatusLabel => RecoveryStatus == PendingCheckoutStatus.ManagerReviewRequired
        ? "REVIEW"
        : "APPROVED";
    public string TerminalLabel => $"Terminal {TerminalId.ToString("N")[..6].ToUpperInvariant()}";
    public string CreatedAtText => CreatedAtLocal.ToString("yyyy-MM-dd HH:mm");
    public string PaymentApprovedAtText => PaymentApprovedAtLocal?.ToString("yyyy-MM-dd HH:mm") ?? "-";
    public string ApprovedAmountText => $"{ApprovedAmount:N0} KRW";
    public string CartSubtotalText => $"{CartSubtotal:N0} KRW";
    public string DiscountAmountText => $"-{DiscountAmount:N0} KRW";
    public string CartTotalText => $"{CartTotal:N0} KRW";
    public string OrderIdText => OrderId?.ToString("N") ?? "Pending";
}

public sealed class CheckoutRecoveryLineViewModel(CheckoutRecoveryLine line)
{
    public string ProductName { get; } = line.ProductName;
    public int Quantity { get; } = line.Quantity;
    public decimal UnitPrice { get; } = line.UnitPrice;
    public decimal LineTotal { get; } = line.LineTotal;
    public string QuantityText => $"x {Quantity:N0}";
    public string UnitPriceText => $"{UnitPrice:N0} KRW";
    public string LineTotalText => $"{LineTotal:N0} KRW";
}
