namespace RetailPOS.Application.Persistence;

public interface IPendingCheckoutRepository
{
    Task SaveAsync(PendingCheckoutRecord checkout, CancellationToken cancellationToken = default);
    Task<PendingCheckoutRecord?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PendingCheckoutRecord>> GetUnresolvedAsync(CancellationToken cancellationToken = default);
    Task MarkCompletedAsync(
        Guid id,
        Guid orderId,
        DateTimeOffset completedAtUtc,
        CancellationToken cancellationToken = default);
    Task MarkManagerReviewRequiredAsync(
        Guid id,
        DateTimeOffset updatedAtUtc,
        CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
