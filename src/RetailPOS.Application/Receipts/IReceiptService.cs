using RetailPOS.Application.Checkout;
using RetailPOS.Application.Persistence;
using RetailPOS.Domain.Orders;
using RetailPOS.Domain.Payments;
using RetailPOS.Domain.Receipts;

namespace RetailPOS.Application.Receipts;

public interface IReceiptService
{
    Task<ReceiptPreview> GenerateAsync(
        Guid localOrderId,
        CancellationToken cancellationToken = default);
}

public sealed record ReceiptPreview(
    string StoreName,
    string StoreAddress,
    string OrderNumber,
    string CashierName,
    string RegisterName,
    DateTimeOffset IssuedAtUtc,
    DateOnly BusinessDate,
    IReadOnlyList<ReceiptPreviewLine> Lines,
    IReadOnlyList<ReceiptPreviewPayment> Payments,
    decimal SubtotalAmount,
    decimal DiscountAmount,
    decimal TotalAmount,
    string PlainText);

public sealed record ReceiptPreviewLine(
    string ProductName,
    decimal UnitPrice,
    int Quantity,
    decimal GrossAmount,
    decimal DiscountAmount,
    decimal TotalAmount);

public sealed record ReceiptPreviewPayment(
    PaymentMethod Method,
    decimal Amount,
    string? ApprovalCode,
    decimal? CashTenderedAmount = null,
    decimal? ChangeAmount = null);

public sealed class ReceiptService(
    IOrderRepository orderRepository,
    IReceiptContextProvider receiptContextProvider,
    ICheckoutClock clock) : IReceiptService
{
    public async Task<ReceiptPreview> GenerateAsync(
        Guid localOrderId,
        CancellationToken cancellationToken = default)
    {
        var order = await orderRepository.GetByIdAsync(localOrderId, cancellationToken)
            ?? throw new KeyNotFoundException($"Order '{localOrderId}' was not found.");

        if (order.Status != OrderStatus.Completed)
        {
            throw new InvalidOperationException("Only completed orders can produce receipts.");
        }

        var approvedPayments = order.Payments
            .Where(payment => payment.Status == PaymentStatus.Approved && payment.ApprovedAmount is not null)
            .ToArray();
        if (approvedPayments.Length == 0)
        {
            throw new InvalidOperationException("A receipt requires an approved payment.");
        }

        var context = receiptContextProvider.GetCurrent(order);
        var receipt = new Receipt(
            context.StoreName,
            context.StoreAddress,
            order.LocalOrderNumber,
            context.CashierName,
            context.RegisterName,
            EnsureUtc(clock.UtcNow, nameof(clock.UtcNow)),
            order.Lines.Select(line => new ReceiptLine(
                line.ProductNameSnapshot,
                line.UnitPrice,
                line.Quantity,
                line.LineDiscountAmount)),
            approvedPayments.Select(payment => new ReceiptPaymentSummary(
                payment.Method,
                payment.ApprovedAmount!.Value,
                payment.ApprovalCode,
                payment.CashTenderedAmount,
                payment.ChangeAmount)));

        return ToPreview(receipt, order.BusinessDate);
    }

    private static ReceiptPreview ToPreview(Receipt receipt, DateOnly businessDate)
    {
        var lines = receipt.Lines.Select(line => new ReceiptPreviewLine(
            line.ProductName,
            line.UnitPrice,
            line.Quantity,
            line.GrossAmount,
            line.DiscountAmount,
            line.TotalAmount)).ToArray();
        var payments = receipt.Payments.Select(payment => new ReceiptPreviewPayment(
            payment.Method,
            payment.Amount,
            payment.ApprovalCode,
            payment.CashTenderedAmount,
            payment.ChangeAmount)).ToArray();

        return new ReceiptPreview(
            receipt.StoreName,
            receipt.StoreAddress,
            receipt.OrderNumber,
            receipt.CashierName,
            receipt.RegisterName,
            receipt.IssuedAtUtc,
            businessDate,
            lines,
            payments,
            receipt.SubtotalAmount,
            receipt.DiscountAmount,
            receipt.TotalAmount,
            ReceiptTextFormatter.Format(receipt, businessDate));
    }

    private static DateTimeOffset EnsureUtc(DateTimeOffset value, string parameterName)
    {
        if (value.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Timestamp must use UTC.", parameterName);
        }

        return value;
    }
}

public interface IReceiptContextProvider
{
    ReceiptContext GetCurrent(Order order);
}

public sealed record ReceiptContext(
    string StoreName,
    string StoreAddress,
    string CashierName,
    string RegisterName);

public sealed class DemoReceiptContextProvider : IReceiptContextProvider
{
    public ReceiptContext GetCurrent(Order order) => new(
        "Retail Store",
        "Local POS Terminal",
        $"Cashier {order.CashierId.ToString("N")[..6].ToUpperInvariant()}",
        $"Register {order.TerminalId.ToString("N")[..6].ToUpperInvariant()}");
}

internal static class ReceiptTextFormatter
{
    public static string Format(Receipt receipt, DateOnly businessDate)
    {
        var lines = new List<string>
        {
            receipt.StoreName,
            receipt.StoreAddress,
            $"Order: {receipt.OrderNumber}",
            $"Business date: {businessDate:yyyy-MM-dd}",
            $"Issued: {receipt.IssuedAtUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss}",
            $"Cashier: {receipt.CashierName}",
            $"Register: {receipt.RegisterName}",
            new('-', 32)
        };

        foreach (var line in receipt.Lines)
        {
            lines.Add($"{line.ProductName} x{line.Quantity}");
            lines.Add($"  {line.UnitPrice:N0}  gross {line.GrossAmount:N0}");
            if (line.DiscountAmount > 0)
            {
                lines.Add($"  discount -{line.DiscountAmount:N0}");
            }

            lines.Add($"  line total {line.TotalAmount:N0}");
        }

        lines.Add(new('-', 32));
        lines.Add($"Subtotal {receipt.SubtotalAmount:N0}");
        lines.Add($"Discount -{receipt.DiscountAmount:N0}");
        lines.Add($"Total {receipt.TotalAmount:N0}");
        lines.Add(new('-', 32));
        foreach (var payment in receipt.Payments)
        {
            var approval = payment.ApprovalCode is null ? string.Empty : $" ({payment.ApprovalCode})";
            lines.Add($"{payment.Method} {payment.Amount:N0}{approval}");
            if (payment.CashTenderedAmount is not null)
            {
                lines.Add($"  Tendered {payment.CashTenderedAmount:N0}");
                lines.Add($"  Change {payment.ChangeAmount:N0}");
            }
        }

        lines.Add("Thank you for shopping with us.");
        return string.Join(Environment.NewLine, lines);
    }
}
