using RetailPOS.Domain.Orders;

namespace RetailPOS.Application.Persistence;

public interface IDashboardRepository
{
    Task<DashboardSummary> GetSummaryAsync(
        DateOnly businessDate,
        int recentOrderCount,
        CancellationToken cancellationToken = default);
}

public sealed record DashboardSummary(
    int OrderCount,
    decimal NetSales,
    int RecoverableCheckoutCount,
    IReadOnlyList<DashboardRecentOrder> RecentOrders);

public sealed record DashboardRecentOrder(
    string LocalOrderNumber,
    DateTimeOffset CreatedAtUtc,
    OrderStatus Status,
    decimal TotalAmount);
