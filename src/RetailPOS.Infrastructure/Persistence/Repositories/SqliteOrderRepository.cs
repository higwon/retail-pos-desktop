using Microsoft.EntityFrameworkCore;
using RetailPOS.Application.Persistence;
using RetailPOS.Domain.Orders;
using RetailPOS.Infrastructure.Persistence.Entities;
using RetailPOS.Infrastructure.Persistence.Mapping;

namespace RetailPOS.Infrastructure.Persistence.Repositories;

public sealed class SqliteOrderRepository(LocalPosDbContext dbContext) : IOrderRepository
{
    public async Task SaveAsync(Order order, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(order);
        dbContext.Orders.Add(order.ToEntity());
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<Order?> GetByIdAsync(Guid localOrderId, CancellationToken cancellationToken = default)
    {
        var entity = await Query().SingleOrDefaultAsync(
            order => order.LocalOrderId == localOrderId,
            cancellationToken);
        return entity?.ToDomain();
    }

    public async Task<Order?> GetByNumberAsync(
        string localOrderNumber,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(localOrderNumber);
        var normalizedNumber = localOrderNumber.Trim();
        var entity = await Query().SingleOrDefaultAsync(
            order => order.LocalOrderNumber == normalizedNumber,
            cancellationToken);
        return entity?.ToDomain();
    }

    public async Task<IReadOnlyList<Order>> GetRecentAsync(
        int count,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
        var entities = await Query()
            .OrderByDescending(order => order.CreatedAtUtc)
            .ThenByDescending(order => order.LocalOrderId)
            .Take(count)
            .ToListAsync(cancellationToken);
        return entities.Select(order => order.ToDomain()).ToList();
    }

    public Task<bool> ExistsAsync(Guid localOrderId, CancellationToken cancellationToken = default) =>
        dbContext.Orders.AnyAsync(order => order.LocalOrderId == localOrderId, cancellationToken);

    private IQueryable<OrderEntity> Query() => dbContext.Orders
        .AsNoTracking()
        .AsSplitQuery()
        .Include(order => order.Lines)
        .Include(order => order.Payments);
}
