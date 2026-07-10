using RetailPOS.Domain.Payments;

namespace RetailPOS.Application.Payments;

public sealed class LocalPaymentSimulator : IPaymentSimulator
{
    private readonly Func<DateTimeOffset> _utcNow;

    public LocalPaymentSimulator()
        : this(() => DateTimeOffset.UtcNow)
    {
    }

    public LocalPaymentSimulator(Func<DateTimeOffset> utcNow)
    {
        _utcNow = utcNow;
    }

    public Task<PaymentSimulationResult> SimulateAsync(
        PaymentSimulationRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateAmount(request.Amount);

        return Task.FromResult(request.Mode switch
        {
            PaymentSimulationMode.Approve => ApprovedResult(request),
            PaymentSimulationMode.Fail => NonApprovedResult(
                request,
                PaymentStatus.Failed,
                "Payment was declined by the local simulator."),
            PaymentSimulationMode.Timeout => NonApprovedResult(
                request,
                PaymentStatus.Failed,
                "Payment timed out. Keep the cart and try again."),
            PaymentSimulationMode.Cancel => NonApprovedResult(
                request,
                PaymentStatus.Cancelled,
                "Payment was cancelled. Cart was not changed."),
            PaymentSimulationMode.CommunicationError => NonApprovedResult(
                request,
                PaymentStatus.Failed,
                "Payment terminal communication failed. Try again or ask a manager to review checkout status."),
            _ => throw new ArgumentOutOfRangeException(
                nameof(request.Mode),
                request.Mode,
                "Unsupported payment simulation mode.")
        });
    }

    private PaymentSimulationResult ApprovedResult(PaymentSimulationRequest request)
    {
        var approvedAtUtc = EnsureUtc(_utcNow());
        var methodCode = request.Method == PaymentMethod.Card ? "CARD" : "CASH";
        var amountCode = decimal.ToInt64(request.Amount).ToString("000000000000");
        var approvalCode = $"APP-{methodCode}-{amountCode}";
        var transactionReference = $"SIM-{methodCode}-{approvedAtUtc:yyyyMMddHHmmss}-{amountCode}";

        return new PaymentSimulationResult(
            PaymentStatus.Approved,
            request.Method,
            request.Amount,
            request.Amount,
            approvalCode,
            transactionReference,
            approvedAtUtc,
            null);
    }

    private static PaymentSimulationResult NonApprovedResult(
        PaymentSimulationRequest request,
        PaymentStatus status,
        string message) =>
        new(
            status,
            request.Method,
            request.Amount,
            null,
            null,
            null,
            null,
            message);

    private static void ValidateAmount(decimal amount)
    {
        if (amount <= 0 || decimal.Truncate(amount) != amount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(amount),
                "Payment amount must be a positive whole-KRW value.");
        }
    }

    private static DateTimeOffset EnsureUtc(DateTimeOffset timestamp)
    {
        if (timestamp.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Payment approval timestamp must use UTC.", nameof(timestamp));
        }

        return timestamp;
    }
}
