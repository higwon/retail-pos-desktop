using RetailPOS.Domain.Common;
using RetailPOS.Domain.Payments;

namespace RetailPOS.Domain.Receipts;

public sealed class ReceiptPaymentSummary
{
    public ReceiptPaymentSummary(PaymentMethod method, decimal amount, string? approvalCode = null)
    {
        Method = method;
        Amount = DomainGuard.Money(amount, nameof(amount), allowZero: false);
        ApprovalCode = string.IsNullOrWhiteSpace(approvalCode) ? null : approvalCode.Trim();
    }

    public PaymentMethod Method { get; }
    public decimal Amount { get; }
    public string? ApprovalCode { get; }
}
