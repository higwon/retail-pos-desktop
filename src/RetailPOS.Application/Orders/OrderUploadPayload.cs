using RetailPOS.Domain.Orders;
using RetailPOS.Domain.Payments;

namespace RetailPOS.Application.Orders;

public sealed record OrderUploadPayload(
    int SchemaVersion,
    Guid StoreId,
    Guid TerminalId,
    Guid LocalOrderId,
    string IdempotencyKey,
    string LocalOrderNumber,
    DateOnly BusinessDate,
    Guid CashierId,
    decimal SubtotalAmount,
    decimal DiscountAmount,
    decimal TotalAmount,
    DateTimeOffset CreatedAt,
    IReadOnlyList<OrderUploadLinePayload> Lines,
    IReadOnlyList<OrderUploadPaymentPayload> Payments)
{
    public const int CurrentSchemaVersion = 1;

    public static OrderUploadPayload From(Order order, string idempotencyKey) => new(
        CurrentSchemaVersion,
        order.StoreId,
        order.TerminalId,
        order.LocalOrderId,
        idempotencyKey,
        order.LocalOrderNumber,
        order.BusinessDate,
        order.CashierId,
        order.SubtotalAmount,
        order.DiscountAmount,
        order.TotalAmount,
        order.CreatedAtUtc,
        order.Lines.Select(OrderUploadLinePayload.From).ToArray(),
        order.Payments.Select(OrderUploadPaymentPayload.From).ToArray());
}

public sealed record OrderUploadLinePayload(
    Guid ProductId,
    string ProductNameSnapshot,
    decimal UnitPrice,
    int Quantity,
    decimal LineDiscountAmount,
    decimal LineTotalAmount)
{
    public static OrderUploadLinePayload From(OrderLine line) => new(
        line.ProductId,
        line.ProductNameSnapshot,
        line.UnitPrice,
        line.Quantity,
        line.LineDiscountAmount,
        line.LineTotalAmount);
}

public sealed record OrderUploadPaymentPayload(
    string PaymentMethod,
    decimal ApprovedAmount,
    string ApprovalCode,
    string? TransactionReference,
    DateTimeOffset ApprovedAtUtc,
    decimal? CashTenderedAmount = null,
    decimal? ChangeAmount = null)
{
    public static OrderUploadPaymentPayload From(Payment payment)
    {
        if (payment.Status != PaymentStatus.Approved ||
            payment.ApprovedAmount is null ||
            payment.ApprovedAtUtc is null)
        {
            throw new InvalidOperationException("Only approved payments can be added to the order upload payload.");
        }

        return new OrderUploadPaymentPayload(
            payment.Method.ToString(),
            payment.ApprovedAmount.Value,
            RequiredText(payment.ApprovalCode, nameof(payment.ApprovalCode)),
            payment.TransactionReference,
            payment.ApprovedAtUtc.Value,
            payment.CashTenderedAmount,
            payment.ChangeAmount);
    }

    private static string RequiredText(string? value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"{parameterName} is required for the order upload payload.");
        }

        return value;
    }
}
