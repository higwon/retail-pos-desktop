using RetailPOS.Domain.Payments;

namespace RetailPOS.Application.Payments;

public interface IPaymentSimulator
{
    Task<PaymentSimulationResult> SimulateAsync(
        PaymentSimulationRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record PaymentSimulationRequest(
    PaymentMethod Method,
    decimal Amount,
    PaymentSimulationMode Mode = PaymentSimulationMode.Approve);

public enum PaymentSimulationMode
{
    Approve,
    Fail
}

public sealed record PaymentSimulationResult(
    PaymentStatus Status,
    PaymentMethod Method,
    decimal RequestedAmount,
    decimal? ApprovedAmount,
    string? ApprovalCode,
    string? TransactionReference,
    DateTimeOffset? ApprovedAtUtc,
    string? FailureMessage)
{
    public bool IsApproved => Status == PaymentStatus.Approved;
}
