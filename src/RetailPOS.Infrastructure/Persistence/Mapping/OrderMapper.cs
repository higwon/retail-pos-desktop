using RetailPOS.Domain.Orders;
using RetailPOS.Domain.Payments;
using RetailPOS.Infrastructure.Persistence.Entities;

namespace RetailPOS.Infrastructure.Persistence.Mapping;

internal static class OrderMapper
{
    public static OrderEntity ToEntity(this Order order) => new()
    {
        LocalOrderId = order.LocalOrderId,
        LocalOrderNumber = order.LocalOrderNumber,
        StoreId = order.StoreId,
        TerminalId = order.TerminalId,
        CashierId = order.CashierId,
        BusinessDate = order.BusinessDate,
        CreatedAtUtc = UtcTime.ToStorage(order.CreatedAtUtc, nameof(order.CreatedAtUtc)),
        Status = (int)order.Status,
        SubtotalAmount = order.SubtotalAmount,
        DiscountAmount = order.DiscountAmount,
        TotalAmount = order.TotalAmount,
        Lines = order.Lines.Select((line, index) => new OrderLineEntity
        {
            Id = Guid.NewGuid(),
            LocalOrderId = order.LocalOrderId,
            SortOrder = index,
            ProductId = line.ProductId,
            ProductNameSnapshot = line.ProductNameSnapshot,
            UnitPrice = line.UnitPrice,
            Quantity = line.Quantity,
            GrossAmount = line.GrossAmount,
            LineDiscountAmount = line.LineDiscountAmount,
            LineTotalAmount = line.LineTotalAmount
        }).ToList(),
        Payments = order.Payments.Select((payment, index) => new PaymentEntity
        {
            Id = payment.Id,
            LocalOrderId = order.LocalOrderId,
            SortOrder = index,
            Method = (int)payment.Method,
            Status = (int)payment.Status,
            RequestedAmount = payment.RequestedAmount,
            ApprovedAmount = payment.ApprovedAmount,
            CashTenderedAmount = payment.CashTenderedAmount,
            ChangeAmount = payment.ChangeAmount,
            CreatedAtUtc = UtcTime.ToStorage(payment.CreatedAtUtc, nameof(payment.CreatedAtUtc)),
            ApprovedAtUtc = UtcTime.ToStorage(payment.ApprovedAtUtc, nameof(payment.ApprovedAtUtc)),
            ApprovalCode = payment.ApprovalCode,
            TransactionReference = payment.TransactionReference,
            FailureReason = payment.FailureReason
        }).ToList()
    };

    public static Order ToDomain(this OrderEntity entity)
    {
        var lines = entity.Lines
            .OrderBy(line => line.SortOrder)
            .Select(line => new OrderLine(
                line.ProductId,
                line.ProductNameSnapshot,
                line.UnitPrice,
                line.Quantity,
                line.LineDiscountAmount));
        var payments = entity.Payments
            .OrderBy(payment => payment.SortOrder)
            .Select(ToDomain);

        return new Order(
            entity.LocalOrderId,
            entity.LocalOrderNumber,
            entity.StoreId,
            entity.TerminalId,
            entity.CashierId,
            entity.BusinessDate,
            UtcTime.FromStorage(entity.CreatedAtUtc),
            lines,
            payments,
            (OrderStatus)entity.Status);
    }

    private static Payment ToDomain(PaymentEntity entity)
    {
        var payment = new Payment(
            entity.Id,
            (PaymentMethod)entity.Method,
            entity.RequestedAmount,
            UtcTime.FromStorage(entity.CreatedAtUtc));

        switch ((PaymentStatus)entity.Status)
        {
            case PaymentStatus.Approved:
                payment.Approve(
                    entity.ApprovedAmount!.Value,
                    UtcTime.FromStorage(entity.ApprovedAtUtc!.Value),
                    entity.ApprovalCode,
                    entity.TransactionReference,
                    entity.CashTenderedAmount,
                    entity.ChangeAmount);
                break;
            case PaymentStatus.Failed:
                payment.Fail(entity.FailureReason!);
                break;
            case PaymentStatus.Cancelled:
                payment.Cancel();
                break;
            case PaymentStatus.Pending:
                break;
            default:
                throw new InvalidOperationException($"Unsupported payment status: {entity.Status}.");
        }

        return payment;
    }
}
