using RetailPOS.Domain.Common;

namespace RetailPOS.Domain.Payments;

public sealed class Payment
{
    public Payment(Guid id, PaymentMethod method, decimal requestedAmount, DateTimeOffset createdAtUtc)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Payment identity is required.", nameof(id));
        }

        Id = id;
        Method = method;
        RequestedAmount = DomainGuard.Money(requestedAmount, nameof(requestedAmount), allowZero: false);
        CreatedAtUtc = DomainGuard.Utc(createdAtUtc, nameof(createdAtUtc));
        Status = PaymentStatus.Pending;
    }

    public Guid Id { get; }
    public PaymentMethod Method { get; }
    public decimal RequestedAmount { get; }
    public DateTimeOffset CreatedAtUtc { get; }
    public PaymentStatus Status { get; private set; }
    public decimal? ApprovedAmount { get; private set; }
    public DateTimeOffset? ApprovedAtUtc { get; private set; }
    public string? ApprovalCode { get; private set; }
    public string? TransactionReference { get; private set; }
    public string? FailureReason { get; private set; }

    public void Approve(decimal approvedAmount, DateTimeOffset approvedAtUtc,
        string? approvalCode = null, string? transactionReference = null)
    {
        EnsurePending();
        approvedAmount = DomainGuard.Money(approvedAmount, nameof(approvedAmount), allowZero: false);
        if (approvedAmount > RequestedAmount)
        {
            throw new ArgumentOutOfRangeException(nameof(approvedAmount), "Approved amount cannot exceed the requested amount.");
        }

        approvedAtUtc = DomainGuard.Utc(approvedAtUtc, nameof(approvedAtUtc));
        var normalizedApprovalCode = string.IsNullOrWhiteSpace(approvalCode) ? null : approvalCode.Trim();
        var normalizedTransactionReference = string.IsNullOrWhiteSpace(transactionReference)
            ? null
            : transactionReference.Trim();

        ApprovedAmount = approvedAmount;
        ApprovedAtUtc = approvedAtUtc;
        ApprovalCode = normalizedApprovalCode;
        TransactionReference = normalizedTransactionReference;
        Status = PaymentStatus.Approved;
    }

    public void Fail(string reason)
    {
        EnsurePending();
        FailureReason = DomainGuard.Required(reason, nameof(reason));
        Status = PaymentStatus.Failed;
    }

    public void Cancel()
    {
        EnsurePending();
        Status = PaymentStatus.Cancelled;
    }

    private void EnsurePending()
    {
        if (Status != PaymentStatus.Pending)
        {
            throw new InvalidOperationException("Only a pending payment can change state.");
        }
    }
}
