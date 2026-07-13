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

    public async Task<CheckoutPaymentExecutionResult> ExecuteAsync(
        PaymentMethod method,
        CancellationToken cancellationToken = default)
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
            var result = await paymentStartService.StartAsync(
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
                activeToken.ThrowIfCancellationRequested();

                try
                {
                    var receipt = await receiptService.GenerateAsync(
                        completion.LocalOrderId,
                        activeToken);
                    activeToken.ThrowIfCancellationRequested();
                    receiptPreviewState.Set(receipt);
                }
                catch (OperationCanceledException) when (activeToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception)
                {
                    successMessage = "Payment approved. Receipt preview could not be generated automatically.";
                }

                checkoutSession.Clear();
                displayState.ShowCompleted();
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
