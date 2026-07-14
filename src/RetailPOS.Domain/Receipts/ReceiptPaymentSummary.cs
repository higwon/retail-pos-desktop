using RetailPOS.Domain.Common;
using RetailPOS.Domain.Payments;

namespace RetailPOS.Domain.Receipts;

public sealed class ReceiptPaymentSummary
{
    public ReceiptPaymentSummary(
        PaymentMethod method,
        decimal amount,
        string? approvalCode = null,
        decimal? cashTenderedAmount = null,
        decimal? changeAmount = null)
    {
        Method = method;
        Amount = DomainGuard.Money(amount, nameof(amount), allowZero: false);
        ApprovalCode = string.IsNullOrWhiteSpace(approvalCode) ? null : approvalCode.Trim();
        if (method != PaymentMethod.Cash &&
            (cashTenderedAmount is not null || changeAmount is not null))
        {
            throw new ArgumentException("Cash tender metadata is only valid for cash receipt payments.");
        }

        if ((cashTenderedAmount is null) != (changeAmount is null))
        {
            throw new ArgumentException("Cash tendered and change amounts must be provided together.");
        }

        if (cashTenderedAmount is not null)
        {
            CashTenderedAmount = DomainGuard.Money(
                cashTenderedAmount.Value,
                nameof(cashTenderedAmount),
                allowZero: false);
            ChangeAmount = DomainGuard.Money(
                changeAmount!.Value,
                nameof(changeAmount),
                allowZero: true);
            if (CashTenderedAmount < Amount || ChangeAmount != CashTenderedAmount - Amount)
            {
                throw new ArgumentException("Cash change must equal tendered amount minus payment amount.");
            }
        }
    }

    public PaymentMethod Method { get; }
    public decimal Amount { get; }
    public string? ApprovalCode { get; }
    public decimal? CashTenderedAmount { get; }
    public decimal? ChangeAmount { get; }
}
