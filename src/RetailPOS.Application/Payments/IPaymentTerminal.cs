using RetailPOS.Domain.Payments;

namespace RetailPOS.Application.Payments;

public interface IPaymentTerminal
{
    Task<PaymentAuthorizationResult> AuthorizeAsync(
        PaymentAuthorizationRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record PaymentAuthorizationRequest(
    Guid PaymentAttemptId,
    decimal Amount);

public sealed record PaymentAuthorizationResult(
    PaymentStatus Status,
    decimal RequestedAmount,
    decimal? ApprovedAmount,
    string? ApprovalCode,
    string? TransactionReference,
    DateTimeOffset? ApprovedAtUtc,
    string? FailureMessage)
{
    public bool IsApproved => Status == PaymentStatus.Approved;
    public bool IsUnknown => Status == PaymentStatus.Unknown;
}
