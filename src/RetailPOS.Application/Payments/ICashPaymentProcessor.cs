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
    decimal Amount);

public sealed class LocalCashPaymentProcessor(TimeProvider timeProvider) : ICashPaymentProcessor
{
    public Task<PaymentAuthorizationResult> AcceptAsync(
        CashPaymentRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Validate(request.PaymentAttemptId, request.Amount);
        var approvedAtUtc = timeProvider.GetUtcNow();
        var amountCode = decimal.ToInt64(request.Amount).ToString("000000000000");

        return Task.FromResult(new PaymentAuthorizationResult(
            PaymentStatus.Approved,
            request.Amount,
            request.Amount,
            $"APP-CASH-{amountCode}",
            $"CASH-{request.PaymentAttemptId:N}",
            approvedAtUtc,
            null));
    }

    private static void Validate(Guid paymentAttemptId, decimal amount)
    {
        if (paymentAttemptId == Guid.Empty)
        {
            throw new ArgumentException("Payment attempt identity is required.", nameof(paymentAttemptId));
        }

        if (amount <= 0m || decimal.Truncate(amount) != amount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(amount),
                "Cash payment amount must be a positive whole-KRW value.");
        }
    }
}
