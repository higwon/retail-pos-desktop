using CommunityToolkit.Mvvm.ComponentModel;
using RetailPOS.Application.Checkout;
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
    private decimal _total;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private string _paymentMessage = string.Empty;

    [ObservableProperty]
    private bool _hasDiscount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasItems))]
    private bool _isEmpty;

    public bool HasItems => !IsEmpty;
    public string TotalAmount => $"{Total:N0} KRW";
    public string ItemSummary => $"{ItemCount:N0} items";
    public string DiscountSummary => $"Discount -{DiscountAmount:N0} KRW";

    partial void OnTotalChanged(decimal value) => OnPropertyChanged(nameof(TotalAmount));
    partial void OnItemCountChanged(int value) => OnPropertyChanged(nameof(ItemSummary));
    partial void OnDiscountAmountChanged(decimal value) => OnPropertyChanged(nameof(DiscountSummary));

    private void OnCheckoutChanged(object? sender, EventArgs e) => RefreshCart();

    private void OnDisplayStateChanged(object? sender, EventArgs e) => RefreshDisplayState();

    private void RefreshCart()
    {
        var snapshot = _checkoutSession.Snapshot;
        Lines.Clear();
        foreach (var line in snapshot.Lines)
        {
            Lines.Add(new CustomerDisplayLineViewModel(
                line.ProductName,
                line.Quantity,
                line.LineTotal));
        }

        ItemCount = snapshot.ItemCount;
        DiscountAmount = snapshot.DiscountAmount;
        HasDiscount = DiscountAmount > 0m;
        Total = snapshot.Total;
        IsEmpty = snapshot.IsEmpty;
    }

    private void RefreshDisplayState()
    {
        var snapshot = _displayState.Snapshot;
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
    string ProductName,
    int Quantity,
    decimal LineTotal)
{
    public string QuantityText => $"Qty {Quantity:N0}";
    public string LineTotalText => $"{LineTotal:N0} KRW";
}
