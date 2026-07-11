using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RetailPOS.Application.Checkout;
using RetailPOS.Application.Payments;
using RetailPOS.Application.Receipts;
using RetailPOS.Domain.Payments;

namespace RetailPOS.Desktop.ViewModels;

public sealed partial class PaymentDialogViewModel : ObservableObject, IDisposable
{
    private readonly CheckoutSession _checkoutSession;
    private readonly IRecoverablePaymentStartService _paymentStartService;
    private readonly IOrderCompletionService _orderCompletionService;
    private readonly IReceiptService _receiptService;
    private readonly ReceiptPreviewState _receiptPreviewState;
    private readonly CheckoutDisplayState _displayState;
    private bool _paymentCompleted;
    private bool _disposed;

    public PaymentDialogViewModel(
        CheckoutSession checkoutSession,
        IRecoverablePaymentStartService paymentStartService,
        IOrderCompletionService orderCompletionService,
        IReceiptService receiptService,
        ReceiptPreviewState receiptPreviewState,
        CheckoutDisplayState displayState)
    {
        _checkoutSession = checkoutSession;
        _paymentStartService = paymentStartService;
        _orderCompletionService = orderCompletionService;
        _receiptService = receiptService;
        _receiptPreviewState = receiptPreviewState;
        _displayState = displayState;
        ApproveCardPaymentCommand = new AsyncRelayCommand(
            cancellationToken => StartPaymentAsync(PaymentMethod.Card, cancellationToken),
            CanStartPayment);
        ApproveCashPaymentCommand = new AsyncRelayCommand(
            cancellationToken => StartPaymentAsync(PaymentMethod.Cash, cancellationToken),
            CanStartPayment);
        _checkoutSession.Changed += OnCheckoutChanged;
        RefreshAmount();
    }

    public IAsyncRelayCommand ApproveCardPaymentCommand { get; }
    public IAsyncRelayCommand ApproveCashPaymentCommand { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TotalAmount))]
    [NotifyPropertyChangedFor(nameof(CanPay))]
    private decimal _amountDue;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasResult))]
    [NotifyPropertyChangedFor(nameof(IsApproved))]
    [NotifyPropertyChangedFor(nameof(IsFailed))]
    [NotifyPropertyChangedFor(nameof(IsUnknown))]
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

    [ObservableProperty]
    private bool _isPaymentInProgress;

    public string TotalAmount => $"{AmountDue:N0} KRW";
    public bool CanPay => AmountDue > 0;
    public bool HasResult => Status is not null;
    public bool IsApproved => Status == PaymentStatus.Approved;
    public bool IsFailed => Status is PaymentStatus.Failed or PaymentStatus.Cancelled;
    public bool IsUnknown => Status == PaymentStatus.Unknown;

    private async Task StartPaymentAsync(PaymentMethod method, CancellationToken cancellationToken)
    {
        IsPaymentInProgress = true;
        Status = null;
        Message = method == PaymentMethod.Card
            ? "Authorization request sent. Waiting for terminal response..."
            : "Recording cash payment...";
        NotifyCommandStateChanged();

        try
        {
            string? successMessage = null;
            _displayState.ShowPaymentWaiting(method, AmountDue);
            var result = await _paymentStartService.StartAsync(
                _checkoutSession.Snapshot,
                method,
                cancellationToken);

            if (!CanApplyResult(cancellationToken))
            {
                return;
            }

            if (result.IsApproved)
            {
                var completion = await _orderCompletionService.CompleteAsync(
                    result.PendingCheckoutId,
                    cancellationToken);

                if (!CanApplyResult(cancellationToken))
                {
                    return;
                }

                try
                {
                    var receipt = await _receiptService.GenerateAsync(
                        completion.LocalOrderId,
                        cancellationToken);

                    if (!CanApplyResult(cancellationToken))
                    {
                        return;
                    }

                    _receiptPreviewState.Set(receipt);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception)
                {
                    successMessage = "Payment approved. Receipt preview could not be generated automatically.";
                }

                _paymentCompleted = true;
                _checkoutSession.Clear();
                _displayState.ShowCompleted();
            }
            else
            {
                _displayState.ShowPaymentFailed(result.FailureMessage ?? "Payment failed.");
            }

            ApplyResult(result, successMessage);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Closing the dialog cancels the command. The persisted checkout state owns any uncertain outcome.
        }
        catch (ArgumentOutOfRangeException) when (!_disposed)
        {
            SetFailure(method, "Payment requires a positive whole-KRW total.");
        }
        catch (Exception) when (!_disposed)
        {
            SetFailure(method,
                "Payment could not be completed. Keep the cart and try again or ask a manager to review checkout status.");
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

    private bool CanApplyResult(CancellationToken cancellationToken) =>
        !_disposed && !cancellationToken.IsCancellationRequested;

    private void ApplyResult(RecoverablePaymentStartResult result, string? successMessage)
    {
        Method = result.Method;
        Status = result.PaymentStatus;
        ApprovedAmount = result.ApprovedAmount;
        ApprovalCode = result.ApprovalCode;
        TransactionReference = result.TransactionReference;
        ApprovedAtUtc = result.ApprovedAtUtc;
        Message = result.IsApproved
            ? successMessage ?? $"{result.Method} payment approved."
            : result.FailureMessage ?? "Payment failed.";
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

    private bool CanStartPayment() =>
        CanPay && !IsPaymentInProgress && !_paymentCompleted && !_disposed;

    private void OnCheckoutChanged(object? sender, EventArgs e) => RefreshAmount();

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _checkoutSession.Changed -= OnCheckoutChanged;
        ApproveCardPaymentCommand.Cancel();
        ApproveCashPaymentCommand.Cancel();
        NotifyCommandStateChanged();
    }

    private void RefreshAmount()
    {
        AmountDue = _checkoutSession.Snapshot.Total;
        NotifyCommandStateChanged();
    }

    private void NotifyCommandStateChanged()
    {
        ApproveCardPaymentCommand.NotifyCanExecuteChanged();
        ApproveCashPaymentCommand.NotifyCanExecuteChanged();
    }
}
