using Microsoft.EntityFrameworkCore;
using RetailPOS.Application.Persistence;
using RetailPOS.Domain.Orders;
using RetailPOS.Domain.Payments;

namespace RetailPOS.Infrastructure.Persistence.Repositories;

public sealed class SqliteReceiptHistoryRepository(LocalPosDbContext dbContext)
    : IReceiptHistoryRepository
{
    public async Task<ReceiptHistoryPageRecord> SearchAsync(
        DateOnly businessDate,
        string? searchText,
        int offset,
        int limit,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);

        var query = dbContext.Orders
            .AsNoTracking()
            .Where(order =>
                order.BusinessDate == businessDate &&
                order.Status == (int)OrderStatus.Completed &&
                order.Payments.Any(payment =>
                    payment.Status == (int)PaymentStatus.Approved &&
                    payment.ApprovedAmount != null));

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            var normalizedSearch = searchText.Trim();
            query = query.Where(order => order.LocalOrderNumber.Contains(normalizedSearch));
        }

        var rows = await query
            .OrderByDescending(order => order.CreatedAtUtc)
            .ThenByDescending(order => order.LocalOrderId)
            .Skip(offset)
            .Take(limit + 1)
            .Select(order => new
            {
                order.LocalOrderId,
                order.LocalOrderNumber,
                order.CreatedAtUtc,
                order.TotalAmount,
                order.CashierId,
                order.TerminalId,
                PaymentMethod = order.Payments
                    .Where(payment =>
                        payment.Status == (int)PaymentStatus.Approved &&
                        payment.ApprovedAmount != null)
                    .OrderBy(payment => payment.SortOrder)
                    .Select(payment => payment.Method)
                    .First()
            })
            .ToListAsync(cancellationToken);

        var hasMore = rows.Count > limit;
        var items = rows.Take(limit).Select(row => new ReceiptHistorySummaryRecord(
            row.LocalOrderId,
            row.LocalOrderNumber,
            new DateTimeOffset(DateTime.SpecifyKind(row.CreatedAtUtc, DateTimeKind.Utc)),
            ToPaymentMethod(row.PaymentMethod),
            row.TotalAmount,
            row.CashierId,
            row.TerminalId)).ToArray();

        return new ReceiptHistoryPageRecord(items, hasMore);
    }

    private static PaymentMethod ToPaymentMethod(int value) =>
        Enum.IsDefined(typeof(PaymentMethod), value)
            ? (PaymentMethod)value
            : throw new InvalidOperationException($"Stored payment method '{value}' is unsupported.");
}
