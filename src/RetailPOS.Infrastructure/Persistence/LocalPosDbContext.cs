using Microsoft.EntityFrameworkCore;
using RetailPOS.Infrastructure.Persistence.Entities;

namespace RetailPOS.Infrastructure.Persistence;

public sealed class LocalPosDbContext(DbContextOptions<LocalPosDbContext> options) : DbContext(options)
{
    public DbSet<ProductEntity> Products => Set<ProductEntity>();
    public DbSet<OrderEntity> Orders => Set<OrderEntity>();
    public DbSet<OrderLineEntity> OrderLines => Set<OrderLineEntity>();
    public DbSet<PaymentEntity> Payments => Set<PaymentEntity>();
    public DbSet<PendingCheckoutEntity> PendingCheckouts => Set<PendingCheckoutEntity>();
    public DbSet<SyncQueueEntity> SyncQueue => Set<SyncQueueEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(LocalPosDbContext).Assembly);
    }
}
