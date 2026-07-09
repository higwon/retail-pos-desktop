using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RetailPOS.Application.Checkout;
using RetailPOS.Domain.Discounts;
using System.Collections.ObjectModel;
using System.Globalization;

namespace RetailPOS.Desktop.ViewModels;

public sealed partial class CartPanelViewModel : ObservableObject, IDisposable
{
    private readonly CheckoutSession _checkoutSession;
    private bool _disposed;

    public CartPanelViewModel(CheckoutSession checkoutSession)
    {
        _checkoutSession = checkoutSession;
        IncreaseQuantityCommand = new RelayCommand<Guid>(checkoutSession.IncreaseQuantity);
        DecreaseQuantityCommand = new RelayCommand<Guid>(checkoutSession.DecreaseQuantity);
        RemoveProductCommand = new RelayCommand<Guid>(checkoutSession.RemoveProduct);
        ClearCommand = new RelayCommand(checkoutSession.Clear, () => HasItems);
        ApplyFixedDiscountCommand = new RelayCommand(ApplyFixedDiscount, () => HasItems);
        ApplyPercentageDiscountCommand = new RelayCommand(ApplyPercentageDiscount, () => HasItems);
        ClearDiscountCommand = new RelayCommand(checkoutSession.ClearDiscount, () => HasDiscount);
        CheckoutCommand = new RelayCommand(RequestCheckout, () => CanCheckout);
        _checkoutSession.Changed += OnCheckoutChanged;
        Refresh();
    }

    public event EventHandler? CheckoutRequested;

    public ObservableCollection<CartLineViewModel> Lines { get; } = [];
    public IRelayCommand<Guid> IncreaseQuantityCommand { get; }
    public IRelayCommand<Guid> DecreaseQuantityCommand { get; }
    public IRelayCommand<Guid> RemoveProductCommand { get; }
    public IRelayCommand ClearCommand { get; }
    public IRelayCommand ApplyFixedDiscountCommand { get; }
    public IRelayCommand ApplyPercentageDiscountCommand { get; }
    public IRelayCommand ClearDiscountCommand { get; }
    public IRelayCommand CheckoutCommand { get; }

    [ObservableProperty]
    private int _itemCount;

    [ObservableProperty]
    private decimal _subtotal;

    [ObservableProperty]
    private decimal _discountAmount;

    [ObservableProperty]
    private string _discountInput = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDiscountError))]
    private string? _discountErrorMessage;

    [ObservableProperty]
    private string? _discountDescription;

    [ObservableProperty]
    private bool _hasDiscount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanCheckout))]
    private decimal _total;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private bool _hasItems;

    public bool IsEmpty => !HasItems;
    public bool HasDiscountError => !string.IsNullOrEmpty(DiscountErrorMessage);
    public bool CanCheckout => Total > 0;

    private void OnCheckoutChanged(object? sender, EventArgs e) => Refresh();

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _checkoutSession.Changed -= OnCheckoutChanged;
        _disposed = true;
    }

    private void Refresh()
    {
        var snapshot = _checkoutSession.Snapshot;
        Lines.Clear();
        foreach (var line in snapshot.Lines)
        {
            Lines.Add(new CartLineViewModel(
                line.ProductId,
                line.ProductName,
                line.UnitPrice,
                line.Quantity,
                line.LineTotal));
        }

        ItemCount = snapshot.ItemCount;
        Subtotal = snapshot.Subtotal;
        DiscountAmount = snapshot.DiscountAmount;
        DiscountDescription = snapshot.DiscountType switch
        {
            DiscountType.FixedAmount => $"Fixed {snapshot.DiscountValue:N0} KRW",
            DiscountType.Percentage => $"{snapshot.DiscountValue:N2}%",
            _ => null
        };
        HasDiscount = snapshot.DiscountType is not null;
        Total = snapshot.Total;
        HasItems = !snapshot.IsEmpty;
        ClearCommand.NotifyCanExecuteChanged();
        ApplyFixedDiscountCommand.NotifyCanExecuteChanged();
        ApplyPercentageDiscountCommand.NotifyCanExecuteChanged();
        ClearDiscountCommand.NotifyCanExecuteChanged();
        CheckoutCommand.NotifyCanExecuteChanged();
    }

    private void RequestCheckout() => CheckoutRequested?.Invoke(this, EventArgs.Empty);

    private void ApplyFixedDiscount() => ApplyDiscount(_checkoutSession.ApplyFixedDiscount);

    private void ApplyPercentageDiscount() => ApplyDiscount(_checkoutSession.ApplyPercentageDiscount);

    private void ApplyDiscount(Action<decimal> apply)
    {
        if (!decimal.TryParse(DiscountInput, NumberStyles.Number, CultureInfo.CurrentCulture, out var value))
        {
            DiscountErrorMessage = "Enter a valid discount value.";
            return;
        }

        try
        {
            apply(value);
            DiscountErrorMessage = null;
        }
        catch (ArgumentException)
        {
            DiscountErrorMessage = "Use a whole non-negative amount or a rate from 0 to 100.";
        }
    }
}

public sealed record CartLineViewModel(
    Guid ProductId,
    string ProductName,
    decimal UnitPrice,
    int Quantity,
    decimal LineTotal);
