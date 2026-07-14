using CommunityToolkit.Mvvm.ComponentModel;
using RetailPOS.Application.Checkout;
using RetailPOS.Domain.Discounts;
using RetailPOS.Domain.Payments;
using System.Collections.ObjectModel;

namespace RetailPOS.Desktop.ViewModels;

public sealed partial class CustomerDisplayViewModel : ObservableObject, IDisposable
{
    private readonly CheckoutSession _checkoutSession;
    private readonly CheckoutDisplayState _displayState;
    private bool _disposed;

    public CustomerDisplayViewModel(CheckoutSession checkoutSession, CheckoutDisplayState displayState)
    {
        _checkoutSession = checkoutSession;
        _displayState = displayState;
        _checkoutSession.Changed += OnCheckoutChanged;
        _displayState.Changed += OnDisplayStateChanged;
        RefreshCart();
        RefreshDisplayState();
    }

    public ObservableCollection<CustomerDisplayLineViewModel> Lines { get; } = [];

    [ObservableProperty]
    private int _itemCount;

    [ObservableProperty]
    private decimal _discountAmount;

    [ObservableProperty]
    private decimal _subtotal;

    [ObservableProperty]
    private decimal _total;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private string _paymentMessage = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsReviewOrderActive))]
    [NotifyPropertyChangedFor(nameof(IsPaymentActive))]
    [NotifyPropertyChangedFor(nameof(IsCompletedActive))]
    [NotifyPropertyChangedFor(nameof(IsPaymentProblem))]
    [NotifyPropertyChangedFor(nameof(IsAmountVisible))]
    [NotifyPropertyChangedFor(nameof(StatusHeading))]
    [NotifyPropertyChangedFor(nameof(StatusAccentBrush))]
    [NotifyPropertyChangedFor(nameof(StatusMutedBrush))]
    private CheckoutDisplayPhase _phase;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PaymentMethodText))]
    private PaymentMethod? _paymentMethod;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AmountToPayText))]
    private decimal? _paymentAmount;

    [ObservableProperty]
    private bool _hasDiscount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasItems))]
    private bool _isEmpty;

    public bool HasItems => !IsEmpty;
    public bool IsReviewOrderActive => Phase == CheckoutDisplayPhase.Cart;
    public bool IsPaymentActive => Phase is CheckoutDisplayPhase.PaymentWaiting or CheckoutDisplayPhase.PaymentFailed;
    public bool IsCompletedActive => Phase == CheckoutDisplayPhase.Completed;
    public bool IsPaymentProblem => Phase == CheckoutDisplayPhase.PaymentFailed;
    public bool IsAmountVisible => !IsCompletedActive;
    public string SubtotalAmount => $"{Subtotal:N0} KRW";
    public string TotalAmount => $"{Total:N0} KRW";
    public string AmountToPayText => $"{PaymentAmount ?? Total:N0} KRW";
    public string ItemSummary => Lines.Count == 1 ? "1 item" : $"{Lines.Count:N0} items";
    public string QuantitySummary => $"Total quantity {ItemCount:N0}";
    public string DiscountLabel { get; private set; } = "Discount";
    public string DiscountSummary => $"-{DiscountAmount:N0} KRW";
    public string PaymentMethodText => PaymentMethod switch
    {
        RetailPOS.Domain.Payments.PaymentMethod.Card => "Card approval",
        RetailPOS.Domain.Payments.PaymentMethod.Cash => "Cash payment",
        _ => "Payment"
    };
    public string StatusHeading => Phase switch
    {
        CheckoutDisplayPhase.PaymentWaiting => "Please wait",
        CheckoutDisplayPhase.PaymentFailed => "Payment needs attention",
        CheckoutDisplayPhase.Completed => "Payment complete",
        _ => "Review your order"
    };
    public string StatusAccentBrush => IsPaymentProblem ? "#FFDC2626" : IsCompletedActive ? "#FF027A48" : "#FF2563EB";
    public string StatusMutedBrush => IsPaymentProblem ? "#FFFEE2E2" : IsCompletedActive ? "#FFECFDF3" : "#FFE8F1FF";

    partial void OnSubtotalChanged(decimal value) => OnPropertyChanged(nameof(SubtotalAmount));
    partial void OnTotalChanged(decimal value)
    {
        OnPropertyChanged(nameof(TotalAmount));
        OnPropertyChanged(nameof(AmountToPayText));
    }
    partial void OnItemCountChanged(int value) => OnPropertyChanged(nameof(QuantitySummary));
    partial void OnDiscountAmountChanged(decimal value) => OnPropertyChanged(nameof(DiscountSummary));

    private void OnCheckoutChanged(object? sender, EventArgs e)
    {
        RefreshCart();
        if (!IsEmpty && _displayState.Snapshot.Phase is
                CheckoutDisplayPhase.Completed or CheckoutDisplayPhase.PaymentFailed)
        {
            _displayState.ShowCart();
        }
    }

    private void OnDisplayStateChanged(object? sender, EventArgs e) => RefreshDisplayState();

    private void RefreshCart()
    {
        var snapshot = _checkoutSession.Snapshot;
        Lines.Clear();
        foreach (var line in snapshot.Lines)
        {
            Lines.Add(new CustomerDisplayLineViewModel(
                line.ProductId,
                line.ProductName,
                line.CategoryName,
                line.UnitPrice,
                line.Quantity,
                line.LineTotal));
        }

        ItemCount = snapshot.ItemCount;
        Subtotal = snapshot.Subtotal;
        DiscountAmount = snapshot.DiscountAmount;
        HasDiscount = DiscountAmount > 0m;
        DiscountLabel = snapshot.DiscountType switch
        {
            DiscountType.FixedAmount => $"Fixed discount ({snapshot.DiscountValue ?? 0m:N0} KRW)",
            DiscountType.Percentage => $"Discount ({snapshot.DiscountValue ?? 0m:0.##}%)",
            _ => "Discount"
        };
        Total = snapshot.Total;
        IsEmpty = snapshot.IsEmpty;
        OnPropertyChanged(nameof(ItemSummary));
        OnPropertyChanged(nameof(DiscountLabel));
    }

    private void RefreshDisplayState()
    {
        var snapshot = _displayState.Snapshot;
        Phase = snapshot.Phase;
        PaymentMethod = snapshot.PaymentMethod;
        PaymentAmount = snapshot.PaymentAmount;
        StatusMessage = snapshot.StatusMessage;
        PaymentMessage = snapshot.PaymentMessage;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _checkoutSession.Changed -= OnCheckoutChanged;
        _displayState.Changed -= OnDisplayStateChanged;
        _disposed = true;
    }
}

public sealed record CustomerDisplayLineViewModel(
    Guid ProductId,
    string ProductName,
    string CategoryName,
    decimal UnitPrice,
    int Quantity,
    decimal LineTotal)
{
    public string UnitPriceText => $"{UnitPrice:N0} KRW";
    public string QuantityText => $"{Quantity:N0}";
    public string LineTotalText => $"{LineTotal:N0} KRW";
}
