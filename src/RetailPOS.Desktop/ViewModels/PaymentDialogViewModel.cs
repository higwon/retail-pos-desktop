using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RetailPOS.Application.Checkout;
using RetailPOS.Application.Payments;
using RetailPOS.Domain.Payments;

namespace RetailPOS.Desktop.ViewModels;

public sealed partial class PaymentDialogViewModel : ObservableObject, IDisposable
{
    private readonly CheckoutSession _checkoutSession;
    private readonly IRecoverablePaymentStartService _paymentStartService;
    private readonly IOrderCompletionService _orderCompletionService;
    private readonly CheckoutDisplayState _displayState;
    private bool _disposed;

    public PaymentDialogViewModel(
        CheckoutSession checkoutSession,
        IRecoverablePaymentStartService paymentStartService,
        IOrderCompletionService orderCompletionService,
        CheckoutDisplayState displayState)
    {
        _checkoutSession = checkoutSession;
        _paymentStartService = paymentStartService;
        _orderCompletionService = orderCompletionService;
        _displayState = displayState;
        ApproveCardPaymentCommand = new AsyncRelayCommand(
            () => SimulateAsync(PaymentMethod.Card, PaymentSimulationMode.Approve),
            CanSimulatePayment);
        ApproveCashPaymentCommand = new AsyncRelayCommand(
            () => SimulateAsync(PaymentMethod.Cash, PaymentSimulationMode.Approve),
            CanSimulatePayment);
        FailPaymentCommand = new AsyncRelayCommand(
            () => SimulateAsync(PaymentMethod.Card, PaymentSimulationMode.Fail),
            CanSimulatePayment);
        _checkoutSession.Changed += OnCheckoutChanged;
        RefreshAmount();
    }

    public IAsyncRelayCommand ApproveCardPaymentCommand { get; }
    public IAsyncRelayCommand ApproveCashPaymentCommand { get; }
    public IAsyncRelayCommand FailPaymentCommand { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TotalAmount))]
    [NotifyPropertyChangedFor(nameof(CanPay))]
    private decimal _amountDue;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasResult))]
    [NotifyPropertyChangedFor(nameof(IsApproved))]
    [NotifyPropertyChangedFor(nameof(IsFailed))]
    private PaymentStatus? _status;

    [ObservableProperty]
    private PaymentMethod? _method;

    [ObservableProperty]
    private decimal? _approvedAmount;

    [ObservableProperty]
    private string? _approvalCode;

    [ObservableProperty]
    private string? _transactionReference;

    [ObservableProperty]
    private DateTimeOffset? _approvedAtUtc;

    [ObservableProperty]
    private string _message = "Choose a method to continue.";

    public string TotalAmount => $"{AmountDue:N0} KRW";
    public bool CanPay => AmountDue > 0;
    public bool HasResult => Status is not null;
    public bool IsApproved => Status == PaymentStatus.Approved;
    public bool IsFailed => Status == PaymentStatus.Failed;

    private async Task SimulateAsync(PaymentMethod method, PaymentSimulationMode mode)
    {
        try
        {
            _displayState.ShowPaymentWaiting(method, AmountDue);
            var result = await _paymentStartService.StartAsync(
                _checkoutSession.Snapshot,
                method,
                mode);

            if (result.IsApproved)
            {
                await _orderCompletionService.CompleteAsync(result.PendingCheckoutId);
                _checkoutSession.Clear();
                _displayState.ShowCompleted();
            }
            else
            {
                _displayState.ShowPaymentFailed(result.FailureMessage ?? "Payment failed.");
            }

            Method = result.Method;
            Status = result.PaymentStatus;
            ApprovedAmount = result.ApprovedAmount;
            ApprovalCode = result.ApprovalCode;
            TransactionReference = result.TransactionReference;
            ApprovedAtUtc = result.ApprovedAtUtc;
            Message = result.IsApproved
                ? $"{result.Method} payment approved."
                : result.FailureMessage ?? "Payment failed.";
        }
        catch (ArgumentOutOfRangeException)
        {
            SetFailure(method, "Payment requires a positive whole-KRW total.");
        }
        catch (Exception)
        {
            SetFailure(method,
                "Payment could not be completed. Keep the cart and try again or ask a manager to review checkout status.");
        }
    }

    private void SetFailure(PaymentMethod method, string message)
    {
        Method = method;
        Status = PaymentStatus.Failed;
        ApprovedAmount = null;
        ApprovalCode = null;
        TransactionReference = null;
        ApprovedAtUtc = null;
        Message = message;
        _displayState.ShowPaymentFailed(message);
    }

    private bool CanSimulatePayment() => CanPay;

    private void OnCheckoutChanged(object? sender, EventArgs e) => RefreshAmount();

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _checkoutSession.Changed -= OnCheckoutChanged;
        _disposed = true;
    }

    private void RefreshAmount()
    {
        AmountDue = _checkoutSession.Snapshot.Total;
        ApproveCardPaymentCommand.NotifyCanExecuteChanged();
        ApproveCashPaymentCommand.NotifyCanExecuteChanged();
        FailPaymentCommand.NotifyCanExecuteChanged();
    }
}
