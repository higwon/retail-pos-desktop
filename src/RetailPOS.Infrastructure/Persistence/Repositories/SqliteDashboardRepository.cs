using Microsoft.EntityFrameworkCore;
using RetailPOS.Application.Persistence;
using RetailPOS.Domain.Orders;

namespace RetailPOS.Infrastructure.Persistence.Repositories;

public sealed class SqliteDashboardRepository(LocalPosDbContext dbContext) : IDashboardRepository
{
    public async Task<DashboardSummary> GetSummaryAsync(
        DateOnly businessDate,
        int recentOrderCount,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(recentOrderCount);

        var businessDayOrders = dbContext.Orders.AsNoTracking()
            .Where(order => order.BusinessDate == businessDate);
        var orderCount = await businessDayOrders.CountAsync(cancellationToken);
        var netSalesValue = await dbContext.Database
            .SqlQuery<long>($"""
                SELECT COALESCE(SUM(CAST("TotalAmount" AS INTEGER)), 0) AS "Value"
                FROM "Orders"
                WHERE "BusinessDate" = {businessDate}
                """)
            .SingleAsync(cancellationToken);
        var netSales = Convert.ToDecimal(netSalesValue);
        var recentOrders = await dbContext.Orders.AsNoTracking()
            .OrderByDescending(order => order.CreatedAtUtc)
            .ThenByDescending(order => order.LocalOrderId)
            .Take(recentOrderCount)
            .Select(order => new DashboardRecentOrder(
                order.LocalOrderNumber,
                new DateTimeOffset(order.CreatedAtUtc, TimeSpan.Zero),
                (OrderStatus)order.Status,
                order.TotalAmount))
            .ToListAsync(cancellationToken);

        var awaitingPayment = (int)PendingCheckoutStatus.AwaitingPayment;
        var approved = (int)PendingCheckoutStatus.ApprovedButOrderNotCreated;
        var managerReview = (int)PendingCheckoutStatus.ManagerReviewRequired;
        var recoverableCheckoutCount = await dbContext.PendingCheckouts.AsNoTracking()
            .CountAsync(checkout =>
                checkout.RecoveryStatus == awaitingPayment ||
                checkout.RecoveryStatus == approved ||
                checkout.RecoveryStatus == managerReview,
                cancellationToken);

        return new DashboardSummary(orderCount, netSales, recoverableCheckoutCount, recentOrders);
    }
}
