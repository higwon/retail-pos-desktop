using RetailPOS.Domain.Common;
using RetailPOS.Domain.Payments;

namespace RetailPOS.Domain.Orders;

public sealed class Order
{
    private readonly IReadOnlyList<OrderLine> _lines;
    private readonly IReadOnlyList<Payment> _payments;

    public Order(Guid localOrderId, string localOrderNumber, Guid storeId, Guid terminalId,
        Guid cashierId, DateOnly businessDate, DateTimeOffset createdAtUtc,
        IEnumerable<OrderLine> lines, IEnumerable<Payment>? payments = null,
        OrderStatus status = OrderStatus.Completed)
    {
        if (localOrderId == Guid.Empty || storeId == Guid.Empty || terminalId == Guid.Empty || cashierId == Guid.Empty)
        {
            throw new ArgumentException("Order, store, terminal, and cashier identities are required.");
        }

        LocalOrderId = localOrderId;
        LocalOrderNumber = DomainGuard.Required(localOrderNumber, nameof(localOrderNumber));
        StoreId = storeId;
        TerminalId = terminalId;
        CashierId = cashierId;
        BusinessDate = businessDate;
        CreatedAtUtc = DomainGuard.Utc(createdAtUtc, nameof(createdAtUtc));
        _lines = (lines ?? throw new ArgumentNullException(nameof(lines))).ToArray();
        if (_lines.Count == 0)
        {
            throw new ArgumentException("An order must contain at least one line.", nameof(lines));
        }

        _payments = payments?.ToArray() ?? [];
        Status = status;
    }

    public Guid LocalOrderId { get; }
    public string LocalOrderNumber { get; }
    public Guid StoreId { get; }
    public Guid TerminalId { get; }
    public Guid CashierId { get; }
    public DateOnly BusinessDate { get; }
    public DateTimeOffset CreatedAtUtc { get; }
    public IReadOnlyList<OrderLine> Lines => _lines;
    public IReadOnlyList<Payment> Payments => _payments;
    public OrderStatus Status { get; }
    public decimal SubtotalAmount => _lines.Sum(line => line.GrossAmount);
    public decimal DiscountAmount => _lines.Sum(line => line.LineDiscountAmount);
    public decimal TotalAmount => _lines.Sum(line => line.LineTotalAmount);
}
