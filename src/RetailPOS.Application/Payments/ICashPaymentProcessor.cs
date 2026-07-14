using RetailPOS.Domain.Payments;

namespace RetailPOS.Application.Payments;

public interface ICashPaymentProcessor
{
    Task<PaymentAuthorizationResult> AcceptAsync(
        CashPaymentRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record CashPaymentRequest(
    Guid PaymentAttemptId,
    decimal AmountDue,
    decimal TenderedAmount);

public sealed class LocalCashPaymentProcessor(TimeProvider timeProvider) : ICashPaymentProcessor
{
    public Task<PaymentAuthorizationResult> AcceptAsync(
        CashPaymentRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Validate(request.PaymentAttemptId, request.AmountDue, request.TenderedAmount);
        var approvedAtUtc = timeProvider.GetUtcNow();
        var amountCode = decimal.ToInt64(request.AmountDue).ToString("000000000000");
        var changeAmount = request.TenderedAmount - request.AmountDue;

        return Task.FromResult(new PaymentAuthorizationResult(
            PaymentStatus.Approved,
            request.AmountDue,
            request.AmountDue,
            $"APP-CASH-{amountCode}",
            $"CASH-{request.PaymentAttemptId:N}",
            approvedAtUtc,
            null,
            request.TenderedAmount,
            changeAmount));
    }

    private static void Validate(Guid paymentAttemptId, decimal amountDue, decimal tenderedAmount)
    {
        if (paymentAttemptId == Guid.Empty)
        {
            throw new ArgumentException("Payment attempt identity is required.", nameof(paymentAttemptId));
        }

        if (amountDue <= 0m || decimal.Truncate(amountDue) != amountDue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(amountDue),
                "Cash payment amount must be a positive whole-KRW value.");
        }

        if (tenderedAmount < amountDue || decimal.Truncate(tenderedAmount) != tenderedAmount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(tenderedAmount),
                "Cash tendered amount must be a whole-KRW value at least equal to the amount due.");
        }
    }
}
