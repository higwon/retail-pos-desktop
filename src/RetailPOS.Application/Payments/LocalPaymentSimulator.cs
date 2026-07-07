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

        if (request.Mode == PaymentSimulationMode.Fail)
        {
            return Task.FromResult(new PaymentSimulationResult(
                PaymentStatus.Failed,
                request.Method,
                request.Amount,
                null,
                null,
                null,
                null,
                "Payment was declined by the local simulator."));
        }

        var approvedAtUtc = EnsureUtc(_utcNow());
        var methodCode = request.Method == PaymentMethod.Card ? "CARD" : "CASH";
        var amountCode = decimal.ToInt64(request.Amount).ToString("000000000000");
        var approvalCode = $"APP-{methodCode}-{amountCode}";
        var transactionReference = $"SIM-{methodCode}-{approvedAtUtc:yyyyMMddHHmmss}-{amountCode}";

        return Task.FromResult(new PaymentSimulationResult(
            PaymentStatus.Approved,
            request.Method,
            request.Amount,
            request.Amount,
            approvalCode,
            transactionReference,
            approvedAtUtc,
            null));
    }

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
