using RetailPOS.Domain.Orders;

namespace RetailPOS.Application.Persistence;

public interface IOrderRepository
{
    Task SaveAsync(Order order, CancellationToken cancellationToken = default);
    Task<Order?> GetByIdAsync(Guid localOrderId, CancellationToken cancellationToken = default);
    Task<Order?> GetByNumberAsync(string localOrderNumber, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Order>> GetRecentAsync(int count, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(Guid localOrderId, CancellationToken cancellationToken = default);
}
