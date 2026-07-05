using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RetailPOS.Application.Checkout;
using System.Collections.ObjectModel;

namespace RetailPOS.Desktop.ViewModels;

public sealed partial class CartPanelViewModel : ObservableObject
{
    private readonly CheckoutSession _checkoutSession;

    public CartPanelViewModel(CheckoutSession checkoutSession)
    {
        _checkoutSession = checkoutSession;
        IncreaseQuantityCommand = new RelayCommand<Guid>(checkoutSession.IncreaseQuantity);
        DecreaseQuantityCommand = new RelayCommand<Guid>(checkoutSession.DecreaseQuantity);
        RemoveProductCommand = new RelayCommand<Guid>(checkoutSession.RemoveProduct);
        ClearCommand = new RelayCommand(checkoutSession.Clear, () => HasItems);
        _checkoutSession.Changed += OnCheckoutChanged;
        Refresh();
    }

    public ObservableCollection<CartLineViewModel> Lines { get; } = [];
    public IRelayCommand<Guid> IncreaseQuantityCommand { get; }
    public IRelayCommand<Guid> DecreaseQuantityCommand { get; }
    public IRelayCommand<Guid> RemoveProductCommand { get; }
    public IRelayCommand ClearCommand { get; }

    [ObservableProperty]
    private int _itemCount;

    [ObservableProperty]
    private decimal _subtotal;

    [ObservableProperty]
    private decimal _discountAmount;

    [ObservableProperty]
    private decimal _total;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private bool _hasItems;

    public bool IsEmpty => !HasItems;

    private void OnCheckoutChanged(object? sender, EventArgs e) => Refresh();

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
        Total = snapshot.Total;
        HasItems = !snapshot.IsEmpty;
        ClearCommand.NotifyCanExecuteChanged();
    }
}

public sealed record CartLineViewModel(
    Guid ProductId,
    string ProductName,
    decimal UnitPrice,
    int Quantity,
    decimal LineTotal);
