using RetailPOS.Domain.Common;

namespace RetailPOS.Domain.Receipts;

public sealed class Receipt
{
    private readonly IReadOnlyList<ReceiptLine> _lines;
    private readonly IReadOnlyList<ReceiptPaymentSummary> _payments;

    public Receipt(string storeName, string storeAddress, string orderNumber,
        string cashierName, string registerName, DateTimeOffset issuedAtUtc,
        IEnumerable<ReceiptLine> lines, IEnumerable<ReceiptPaymentSummary> payments)
    {
        StoreName = DomainGuard.Required(storeName, nameof(storeName));
        StoreAddress = DomainGuard.Required(storeAddress, nameof(storeAddress));
        OrderNumber = DomainGuard.Required(orderNumber, nameof(orderNumber));
        CashierName = DomainGuard.Required(cashierName, nameof(cashierName));
        RegisterName = DomainGuard.Required(registerName, nameof(registerName));
        IssuedAtUtc = DomainGuard.Utc(issuedAtUtc, nameof(issuedAtUtc));
        _lines = (lines ?? throw new ArgumentNullException(nameof(lines))).ToArray();
        _payments = (payments ?? throw new ArgumentNullException(nameof(payments))).ToArray();
        if (_lines.Count == 0 || _payments.Count == 0)
        {
            throw new ArgumentException("A receipt requires at least one line and one payment.");
        }

        if (PaymentTotal != TotalAmount)
        {
            throw new ArgumentException("Receipt payment total must equal the receipt total.", nameof(payments));
        }
    }

    public string StoreName { get; }
    public string StoreAddress { get; }
    public string OrderNumber { get; }
    public string CashierName { get; }
    public string RegisterName { get; }
    public DateTimeOffset IssuedAtUtc { get; }
    public IReadOnlyList<ReceiptLine> Lines => _lines;
    public IReadOnlyList<ReceiptPaymentSummary> Payments => _payments;
    public decimal SubtotalAmount => _lines.Sum(line => line.GrossAmount);
    public decimal DiscountAmount => _lines.Sum(line => line.DiscountAmount);
    public decimal TotalAmount => _lines.Sum(line => line.TotalAmount);
    public decimal PaymentTotal => _payments.Sum(payment => payment.Amount);
}
