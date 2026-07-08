using Microsoft.EntityFrameworkCore;
using RetailPOS.Application.Persistence;
using RetailPOS.Infrastructure.Persistence.Mapping;

namespace RetailPOS.Infrastructure.Persistence.Repositories;

public sealed class SqlitePendingCheckoutRepository(LocalPosDbContext dbContext) : IPendingCheckoutRepository
{
    public async Task SaveAsync(
        PendingCheckoutRecord checkout,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(checkout);
        var existing = await dbContext.PendingCheckouts.FindAsync([checkout.Id], cancellationToken);
        if (existing is null)
        {
            dbContext.PendingCheckouts.Add(checkout.ToEntity());
        }
        else
        {
            dbContext.Entry(existing).CurrentValues.SetValues(checkout.ToEntity());
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<PendingCheckoutRecord?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.PendingCheckouts.AsNoTracking()
            .SingleOrDefaultAsync(checkout => checkout.Id == id, cancellationToken);
        return entity?.ToRecord();
    }

    public async Task<IReadOnlyList<PendingCheckoutRecord>> GetUnresolvedAsync(
        CancellationToken cancellationToken = default)
    {
        var completed = (int)PendingCheckoutStatus.Completed;
        var entities = await dbContext.PendingCheckouts.AsNoTracking()
            .Where(checkout => checkout.RecoveryStatus != completed)
            .OrderBy(checkout => checkout.CreatedAtUtc)
            .ThenBy(checkout => checkout.Id)
            .ToListAsync(cancellationToken);
        return entities.Select(checkout => checkout.ToRecord()).ToList();
    }

    public async Task MarkCompletedAsync(
        Guid id,
        Guid orderId,
        DateTimeOffset completedAtUtc,
        CancellationToken cancellationToken = default)
    {
        var completedAt = UtcTime.ToStorage(completedAtUtc, nameof(completedAtUtc));
        var entity = await dbContext.PendingCheckouts.SingleOrDefaultAsync(
            checkout => checkout.Id == id,
            cancellationToken) ?? throw new KeyNotFoundException($"Pending checkout '{id}' was not found.");
        entity.RecoveryStatus = (int)PendingCheckoutStatus.Completed;
        entity.OrderId = orderId;
        entity.CompletedAtUtc = completedAt;
        entity.LastUpdatedAtUtc = completedAt;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkManagerReviewRequiredAsync(
        Guid id,
        DateTimeOffset updatedAtUtc,
        CancellationToken cancellationToken = default)
    {
        var updatedAt = UtcTime.ToStorage(updatedAtUtc, nameof(updatedAtUtc));
        var entity = await dbContext.PendingCheckouts.SingleOrDefaultAsync(
            checkout => checkout.Id == id,
            cancellationToken) ?? throw new KeyNotFoundException($"Pending checkout '{id}' was not found.");
        entity.RecoveryStatus = (int)PendingCheckoutStatus.ManagerReviewRequired;
        entity.LastUpdatedAtUtc = updatedAt;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.PendingCheckouts.SingleOrDefaultAsync(
            checkout => checkout.Id == id,
            cancellationToken);
        if (entity is not null)
        {
            dbContext.PendingCheckouts.Remove(entity);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
