using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RetailPOS.Application.Checkout;
using RetailPOS.Desktop.Workflow;
using RetailPOS.Domain.Discounts;
using RetailPOS.Domain.Payments;
using System.Collections.ObjectModel;
using System.Globalization;

namespace RetailPOS.Desktop.ViewModels;

public sealed partial class CartPanelViewModel : ObservableObject, IDisposable
{
    private readonly CheckoutSession _checkoutSession;
    private readonly ICheckoutPaymentCoordinator _paymentCoordinator;
    private bool _disposed;

    public CartPanelViewModel(
        CheckoutSession checkoutSession,
        ICheckoutPaymentCoordinator? paymentCoordinator = null)
    {
        _checkoutSession = checkoutSession;
        _paymentCoordinator = paymentCoordinator ?? UnavailableCheckoutPaymentCoordinator.Instance;
        IncreaseQuantityCommand = new RelayCommand<Guid>(checkoutSession.IncreaseQuantity);
        DecreaseQuantityCommand = new RelayCommand<Guid>(checkoutSession.DecreaseQuantity);
        RemoveProductCommand = new RelayCommand<Guid>(checkoutSession.RemoveProduct);
        ClearCommand = new RelayCommand(checkoutSession.Clear, () => HasItems && !IsPaymentInProgress);
        ApplyFixedDiscountCommand = new RelayCommand(ApplyFixedDiscount, () => HasItems && !IsPaymentInProgress);
        ApplyPercentageDiscountCommand = new RelayCommand(ApplyPercentageDiscount, () => HasItems && !IsPaymentInProgress);
        ClearDiscountCommand = new RelayCommand(checkoutSession.ClearDiscount, () => HasDiscount && !IsPaymentInProgress);
        SelectFixedDiscountCommand = new RelayCommand(() => DiscountMode = CartDiscountMode.FixedAmount);
        SelectPercentageDiscountCommand = new RelayCommand(() => DiscountMode = CartDiscountMode.Percentage);
        ApplySelectedDiscountCommand = new RelayCommand(ApplySelectedDiscount, () => HasItems && !IsPaymentInProgress);
        SelectQuickDiscountCommand = new RelayCommand<string>(value => DiscountInput = value ?? "0");
        ResetDiscountCommand = new RelayCommand(ResetDiscount, () => (HasItems || HasDiscount) && !IsPaymentInProgress);
        StartCardPaymentCommand = new AsyncRelayCommand(StartCardPaymentAsync, CanStartPayment);
        CancelCardPaymentCommand = new RelayCommand(CancelCardPayment, () => IsCardPaymentVisible && IsPaymentInProgress);
        BackFromCardPaymentCommand = new RelayCommand(BackFromCardPayment, () => IsCardPaymentVisible && !IsPaymentInProgress);
        CompleteCardPaymentCommand = new RelayCommand(CompleteCardPayment, () => IsCardApproved && !IsPaymentInProgress);
        OpenCashTenderCommand = new RelayCommand(OpenCashTender, CanStartPayment);
        CancelCashTenderCommand = new RelayCommand(CancelCashTender, () => IsCashTenderVisible && !IsPaymentInProgress);
        AppendCashDigitCommand = new RelayCommand<string>(AppendCashDigit, digit => IsCashTenderVisible && !IsPaymentInProgress && digit is not null);
        BackspaceCashCommand = new RelayCommand(BackspaceCash, () => IsCashTenderVisible && !IsPaymentInProgress);
        ClearCashCommand = new RelayCommand(ClearCash, () => IsCashTenderVisible && !IsPaymentInProgress);
        SelectQuickTenderCommand = new RelayCommand<decimal>(SelectQuickTender, _ => IsCashTenderVisible && !IsPaymentInProgress);
        CompleteCashPaymentCommand = new AsyncRelayCommand(CompleteCashPaymentAsync, CanCompleteCashPayment);
        _checkoutSession.Changed += OnCheckoutChanged;
        Refresh();
    }

    public event EventHandler? CardPaymentCompleted;
    public event EventHandler? CashPaymentCompleted;

    public ObservableCollection<CartLineViewModel> Lines { get; } = [];
    public ObservableCollection<CashTenderOption> QuickTenderOptions { get; } = [];
    public IRelayCommand<Guid> IncreaseQuantityCommand { get; }
    public IRelayCommand<Guid> DecreaseQuantityCommand { get; }
    public IRelayCommand<Guid> RemoveProductCommand { get; }
    public IRelayCommand ClearCommand { get; }
    public IRelayCommand ApplyFixedDiscountCommand { get; }
    public IRelayCommand ApplyPercentageDiscountCommand { get; }
    public IRelayCommand ClearDiscountCommand { get; }
    public IRelayCommand SelectFixedDiscountCommand { get; }
    public IRelayCommand SelectPercentageDiscountCommand { get; }
    public IRelayCommand ApplySelectedDiscountCommand { get; }
    public IRelayCommand<string> SelectQuickDiscountCommand { get; }
    public IRelayCommand ResetDiscountCommand { get; }
    public IAsyncRelayCommand StartCardPaymentCommand { get; }
    public IRelayCommand CancelCardPaymentCommand { get; }
    public IRelayCommand BackFromCardPaymentCommand { get; }
    public IRelayCommand CompleteCardPaymentCommand { get; }
    public IRelayCommand OpenCashTenderCommand { get; }
    public IRelayCommand CancelCashTenderCommand { get; }
    public IRelayCommand<string> AppendCashDigitCommand { get; }
    public IRelayCommand BackspaceCashCommand { get; }
    public IRelayCommand ClearCashCommand { get; }
    public IRelayCommand<decimal> SelectQuickTenderCommand { get; }
    public IAsyncRelayCommand CompleteCashPaymentCommand { get; }

    [ObservableProperty]
    private int _itemCount;

    [ObservableProperty]
    private decimal _subtotal;

    [ObservableProperty]
    private decimal _discountAmount;

    [ObservableProperty]
    private string _discountInput = "0";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFixedDiscountMode))]
    [NotifyPropertyChangedFor(nameof(IsPercentageDiscountMode))]
    [NotifyPropertyChangedFor(nameof(DiscountUnitLabel))]
    private CartDiscountMode _discountMode = CartDiscountMode.FixedAmount;

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

    [ObservableProperty]
    private bool _isCashTenderVisible;

    [ObservableProperty]
    private bool _isCardPaymentVisible;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCardApproved))]
    [NotifyPropertyChangedFor(nameof(IsCardFailed))]
    [NotifyPropertyChangedFor(nameof(IsCardUnknown))]
    private PaymentStatus? _cardPaymentStatus;

    [ObservableProperty]
    private string _cardPaymentMessage = "Present the card at the connected terminal.";

    [ObservableProperty]
    private string? _cardApprovalCode;

    [ObservableProperty]
    private string? _cardTransactionReference;

    [ObservableProperty]
    private decimal _cardSubtotal;

    [ObservableProperty]
    private decimal _cardDiscountAmount;

    [ObservableProperty]
    private decimal _cardAmountDue;

    [ObservableProperty]
    private string _cashReceivedInput = "0";

    [ObservableProperty]
    private decimal _cashReceivedAmount;

    [ObservableProperty]
    private decimal _changeDue;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCashTenderError))]
    private string? _cashTenderErrorMessage;

    [ObservableProperty]
    private string? _cashPaymentMessage;

    [ObservableProperty]
    private bool _isPaymentInProgress;

    public bool IsEmpty => !HasItems;
    public bool HasDiscountError => !string.IsNullOrEmpty(DiscountErrorMessage);
    public bool HasCashTenderError => !string.IsNullOrEmpty(CashTenderErrorMessage);
    public bool CanCheckout => Total > 0 && !IsPaymentInProgress;
    public bool IsCardApproved => CardPaymentStatus == PaymentStatus.Approved;
    public bool IsCardFailed => CardPaymentStatus is PaymentStatus.Failed or PaymentStatus.Cancelled;
    public bool IsCardUnknown => CardPaymentStatus == PaymentStatus.Unknown;
    public bool IsFixedDiscountMode => DiscountMode == CartDiscountMode.FixedAmount;
    public bool IsPercentageDiscountMode => DiscountMode == CartDiscountMode.Percentage;
    public string DiscountUnitLabel => IsFixedDiscountMode ? "KRW" : "%";

    partial void OnCashReceivedInputChanged(string value) => RefreshCashTender();

    private void OnCheckoutChanged(object? sender, EventArgs e) => Refresh();

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _checkoutSession.Changed -= OnCheckoutChanged;
        StartCardPaymentCommand.Cancel();
        CompleteCashPaymentCommand.Cancel();
        _disposed = true;
        NotifyCommandStateChanged();
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
                line.CategoryName,
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

        if (!HasItems)
        {
            DiscountInput = "0";
            ResetCashTender();
        }
        else
        {
            RefreshQuickTenderOptions();
            RefreshCashTender();
        }

        NotifyCommandStateChanged();
    }

    private async Task StartCardPaymentAsync(CancellationToken cancellationToken)
    {
        if (!CanStartPayment())
        {
            return;
        }

        IsCashTenderVisible = false;
        IsCardPaymentVisible = true;
        IsPaymentInProgress = true;
        CardPaymentStatus = null;
        CardApprovalCode = null;
        CardTransactionReference = null;
        CardSubtotal = Subtotal;
        CardDiscountAmount = DiscountAmount;
        CardAmountDue = Total;
        CardPaymentMessage = "Authorization request sent. Waiting for terminal response...";
        NotifyCommandStateChanged();

        try
        {
            var execution = await _paymentCoordinator.ExecuteAsync(PaymentMethod.Card, cancellationToken);
            if (_disposed || cancellationToken.IsCancellationRequested)
            {
                return;
            }

            CardPaymentStatus = execution.Payment.PaymentStatus;
            CardApprovalCode = execution.Payment.ApprovalCode;
            CardTransactionReference = execution.Payment.TransactionReference;
            CardPaymentMessage = execution.Message;
        }
        catch (OperationCanceledException) when (!_disposed)
        {
            CardPaymentStatus = PaymentStatus.Unknown;
            CardPaymentMessage =
                "Terminal response is unknown. Review checkout status before attempting another payment.";
        }
        catch (ArgumentOutOfRangeException) when (!_disposed)
        {
            CardPaymentStatus = PaymentStatus.Failed;
            CardPaymentMessage = "Payment requires a positive whole-KRW total.";
        }
        catch (Exception) when (!_disposed)
        {
            CardPaymentStatus = PaymentStatus.Failed;
            CardPaymentMessage =
                "Payment could not be completed. Keep the sale and review checkout status before retrying.";
        }
        finally
        {
            if (!_disposed)
            {
                IsPaymentInProgress = false;
                NotifyCommandStateChanged();
            }
        }
    }

    private void CancelCardPayment() => StartCardPaymentCommand.Cancel();

    private void BackFromCardPayment()
    {
        if (IsPaymentInProgress)
        {
            return;
        }

        ResetCardPayment();
        NotifyCommandStateChanged();
    }

    private void CompleteCardPayment()
    {
        if (!IsCardApproved || IsPaymentInProgress)
        {
            return;
        }

        ResetCardPayment();
        CardPaymentCompleted?.Invoke(this, EventArgs.Empty);
    }

    private void ResetCardPayment()
    {
        IsCardPaymentVisible = false;
        CardPaymentStatus = null;
        CardPaymentMessage = "Present the card at the connected terminal.";
        CardApprovalCode = null;
        CardTransactionReference = null;
        CardSubtotal = 0m;
        CardDiscountAmount = 0m;
        CardAmountDue = 0m;
    }

    private bool CanStartPayment() =>
        CanCheckout && !IsCardPaymentVisible && !IsCashTenderVisible && !_disposed;

    private void OpenCashTender()
    {
        if (!CanStartPayment())
        {
            return;
        }

        IsCashTenderVisible = true;
        ResetCardPayment();
        CashReceivedInput = "0";
        CashPaymentMessage = null;
        RefreshQuickTenderOptions();
        NotifyCommandStateChanged();
    }

    private void CancelCashTender()
    {
        if (IsPaymentInProgress)
        {
            return;
        }

        ResetCashTender();
        NotifyCommandStateChanged();
    }

    private void ResetCashTender()
    {
        IsCashTenderVisible = false;
        CashReceivedInput = "0";
        CashReceivedAmount = 0m;
        ChangeDue = 0m;
        CashTenderErrorMessage = null;
        CashPaymentMessage = null;
        QuickTenderOptions.Clear();
    }

    private void AppendCashDigit(string? digit)
    {
        if (string.IsNullOrEmpty(digit) || digit.Any(character => !char.IsDigit(character)))
        {
            return;
        }

        var current = new string(CashReceivedInput.Where(char.IsDigit).ToArray());
        CashReceivedInput = current == "0" ? digit : current + digit;
    }

    private void BackspaceCash()
    {
        var current = new string(CashReceivedInput.Where(char.IsDigit).ToArray());
        CashReceivedInput = current.Length <= 1 ? "0" : current[..^1];
    }

    private void ClearCash() => CashReceivedInput = "0";

    private void SelectQuickTender(decimal amount) =>
        CashReceivedInput = amount.ToString("0", CultureInfo.CurrentCulture);

    private void RefreshCashTender()
    {
        if (!decimal.TryParse(
                CashReceivedInput,
                NumberStyles.Number,
                CultureInfo.CurrentCulture,
                out var received) ||
            received < 0m ||
            decimal.Truncate(received) != received)
        {
            CashReceivedAmount = 0m;
            ChangeDue = 0m;
            CashTenderErrorMessage = "Enter a non-negative whole-KRW cash amount.";
            CompleteCashPaymentCommand.NotifyCanExecuteChanged();
            return;
        }

        CashReceivedAmount = received;
        ChangeDue = received >= Total ? received - Total : 0m;
        CashTenderErrorMessage = received > 0m && received < Total
            ? $"Cash received is {Total - received:N0} KRW short."
            : null;
        CompleteCashPaymentCommand.NotifyCanExecuteChanged();
    }

    private void RefreshQuickTenderOptions()
    {
        QuickTenderOptions.Clear();
        if (Total <= 0m)
        {
            return;
        }

        var amounts = new[]
        {
            Total,
            RoundUp(Total, 1_000m),
            RoundUp(Total, 5_000m),
            RoundUp(Total, 10_000m)
        };

        foreach (var amount in amounts.Distinct().Take(4))
        {
            QuickTenderOptions.Add(new CashTenderOption(amount, $"{amount:N0}"));
        }
    }

    private static decimal RoundUp(decimal amount, decimal increment)
    {
        try
        {
            return decimal.Ceiling(amount / increment) * increment;
        }
        catch (OverflowException)
        {
            return amount;
        }
    }

    private bool CanCompleteCashPayment() =>
        IsCashTenderVisible &&
        CanCheckout &&
        !HasCashTenderError &&
        CashReceivedAmount >= Total &&
        !_disposed;

    private async Task CompleteCashPaymentAsync(CancellationToken cancellationToken)
    {
        if (!CanCompleteCashPayment())
        {
            return;
        }

        IsPaymentInProgress = true;
        CashPaymentMessage = "Recording cash payment...";
        NotifyCommandStateChanged();
        var paymentCompleted = false;

        try
        {
            var execution = await _paymentCoordinator.ExecuteAsync(
                PaymentMethod.Cash,
                cancellationToken);
            if (cancellationToken.IsCancellationRequested || _disposed)
            {
                return;
            }

            if (execution.Payment.IsApproved)
            {
                CashPaymentMessage = execution.Message;
                paymentCompleted = true;
            }
            else
            {
                CashPaymentMessage = execution.Message;
                CashTenderErrorMessage = execution.Message;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception) when (!_disposed)
        {
            CashPaymentMessage = null;
            CashTenderErrorMessage =
                "Cash payment could not be completed. The sale was kept for review or retry.";
        }
        finally
        {
            if (!_disposed)
            {
                IsPaymentInProgress = false;
                NotifyCommandStateChanged();
            }
        }

        if (paymentCompleted && !_disposed)
        {
            CashPaymentCompleted?.Invoke(this, EventArgs.Empty);
        }
    }

    private void NotifyCommandStateChanged()
    {
        ClearCommand.NotifyCanExecuteChanged();
        ApplyFixedDiscountCommand.NotifyCanExecuteChanged();
        ApplyPercentageDiscountCommand.NotifyCanExecuteChanged();
        ClearDiscountCommand.NotifyCanExecuteChanged();
        ApplySelectedDiscountCommand.NotifyCanExecuteChanged();
        ResetDiscountCommand.NotifyCanExecuteChanged();
        StartCardPaymentCommand.NotifyCanExecuteChanged();
        CancelCardPaymentCommand.NotifyCanExecuteChanged();
        BackFromCardPaymentCommand.NotifyCanExecuteChanged();
        CompleteCardPaymentCommand.NotifyCanExecuteChanged();
        OpenCashTenderCommand.NotifyCanExecuteChanged();
        CancelCashTenderCommand.NotifyCanExecuteChanged();
        AppendCashDigitCommand.NotifyCanExecuteChanged();
        BackspaceCashCommand.NotifyCanExecuteChanged();
        ClearCashCommand.NotifyCanExecuteChanged();
        SelectQuickTenderCommand.NotifyCanExecuteChanged();
        CompleteCashPaymentCommand.NotifyCanExecuteChanged();
    }

    private void ApplyFixedDiscount() => ApplyDiscount(_checkoutSession.ApplyFixedDiscount);

    private void ApplyPercentageDiscount() => ApplyDiscount(_checkoutSession.ApplyPercentageDiscount);

    private void ApplySelectedDiscount()
    {
        if (DiscountMode == CartDiscountMode.FixedAmount)
        {
            ApplyFixedDiscount();
            return;
        }

        ApplyPercentageDiscount();
    }

    private void ResetDiscount()
    {
        _checkoutSession.ClearDiscount();
        DiscountInput = "0";
        DiscountErrorMessage = null;
    }

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

    private sealed class UnavailableCheckoutPaymentCoordinator : ICheckoutPaymentCoordinator
    {
        public static UnavailableCheckoutPaymentCoordinator Instance { get; } = new();

        public Task<CheckoutPaymentExecutionResult> ExecuteAsync(
            PaymentMethod method,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Checkout payment services are unavailable.");

        public void CancelActivePayment()
        {
        }
    }
}

public sealed record CartLineViewModel(
    Guid ProductId,
    string ProductName,
    string CategoryName,
    decimal UnitPrice,
    int Quantity,
    decimal LineTotal)
{
    public string DiscountText => "-";
}

public sealed record CashTenderOption(decimal Amount, string Label);

public enum CartDiscountMode
{
    FixedAmount,
    Percentage
}
