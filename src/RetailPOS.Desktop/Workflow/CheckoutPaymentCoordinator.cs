using RetailPOS.Application.Checkout;
using RetailPOS.Application.Receipts;
using RetailPOS.Desktop.ViewModels;
using RetailPOS.Domain.Payments;

namespace RetailPOS.Desktop.Workflow;

public interface ICheckoutPaymentCoordinator
{
    Task<CheckoutPaymentExecutionResult> ExecuteAsync(
        PaymentMethod method,
        CancellationToken cancellationToken = default);

    Task<CheckoutPaymentExecutionResult> ExecuteCashAsync(
        decimal tenderedAmount,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(PaymentMethod.Cash, cancellationToken);

    void CancelActivePayment();
}

public sealed class CheckoutPaymentCoordinator(
    CheckoutSession checkoutSession,
    IRecoverablePaymentStartService paymentStartService,
    IOrderCompletionService orderCompletionService,
    IReceiptService receiptService,
    ReceiptPreviewState receiptPreviewState,
    CheckoutDisplayState displayState) : ICheckoutPaymentCoordinator
{
    private int _isExecuting;
    private readonly object _cancellationSync = new();
    private CancellationTokenSource? _activeCancellation;

    public Task<CheckoutPaymentExecutionResult> ExecuteAsync(
        PaymentMethod method,
        CancellationToken cancellationToken = default) =>
        ExecuteCoreAsync(method, null, cancellationToken);

    public Task<CheckoutPaymentExecutionResult> ExecuteCashAsync(
        decimal tenderedAmount,
        CancellationToken cancellationToken = default) =>
        ExecuteCoreAsync(PaymentMethod.Cash, tenderedAmount, cancellationToken);

    private async Task<CheckoutPaymentExecutionResult> ExecuteCoreAsync(
        PaymentMethod method,
        decimal? cashTenderedAmount,
        CancellationToken cancellationToken)
    {
        if (Interlocked.CompareExchange(ref _isExecuting, 1, 0) != 0)
        {
            throw new InvalidOperationException("A checkout payment is already in progress.");
        }

        try
        {
            using var linkedCancellation =
                CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            lock (_cancellationSync)
            {
                _activeCancellation = linkedCancellation;
            }

            var activeToken = linkedCancellation.Token;
            var amountDue = checkoutSession.Snapshot.Total;
            displayState.ShowPaymentWaiting(method, amountDue);
            var result = method == PaymentMethod.Cash && cashTenderedAmount is not null
                ? await paymentStartService.StartCashAsync(
                    checkoutSession.Snapshot,
                    cashTenderedAmount.Value,
                    activeToken)
                : await paymentStartService.StartAsync(
                    checkoutSession.Snapshot,
                    method,
                    activeToken);
            activeToken.ThrowIfCancellationRequested();

            string? successMessage = null;
            if (result.IsApproved)
            {
                var completion = await orderCompletionService.CompleteAsync(
                    result.PendingCheckoutId,
                    activeToken);

                checkoutSession.Clear();
                displayState.ShowCompleted();
                receiptPreviewState.Clear();

                try
                {
                    var receipt = await receiptService.GenerateAsync(
                        completion.LocalOrderId,
                        CancellationToken.None);
                    receiptPreviewState.Set(receipt);
                }
                catch (Exception)
                {
                    successMessage = "Payment approved. Receipt preview could not be generated automatically.";
                }
            }
            else
            {
                displayState.ShowPaymentFailed(result.FailureMessage ?? "Payment failed.");
            }

            return new CheckoutPaymentExecutionResult(
                result,
                successMessage ?? ResultMessage(result));
        }
        catch (OperationCanceledException)
        {
            displayState.ShowPaymentFailed(
                "Terminal response is unknown. Review checkout status before retrying.");
            throw;
        }
        catch
        {
            displayState.ShowPaymentFailed(
                "Payment could not be completed. Keep the cart and review checkout status before retrying.");
            throw;
        }
        finally
        {
            lock (_cancellationSync)
            {
                _activeCancellation = null;
            }
            Volatile.Write(ref _isExecuting, 0);
        }
    }

    public void CancelActivePayment()
    {
        lock (_cancellationSync)
        {
            _activeCancellation?.Cancel();
        }
    }

    private static string ResultMessage(RecoverablePaymentStartResult result) =>
        result.IsApproved
            ? $"{result.Method} payment approved."
            : result.FailureMessage ?? "Payment failed.";
}

public sealed record CheckoutPaymentExecutionResult(
    RecoverablePaymentStartResult Payment,
    string Message);
