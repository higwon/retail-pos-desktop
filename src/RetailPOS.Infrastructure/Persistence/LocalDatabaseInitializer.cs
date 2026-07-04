using Microsoft.EntityFrameworkCore;

namespace RetailPOS.Infrastructure.Persistence;

public sealed class LocalDatabaseInitializer(LocalPosDbContext dbContext, ProductSeedData productSeedData)
{
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var connection = dbContext.Database.GetDbConnection();
        if (!string.IsNullOrWhiteSpace(connection.DataSource))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(connection.DataSource))!);
        }

        await dbContext.Database.MigrateAsync(cancellationToken);
        await productSeedData.SeedAsync(cancellationToken);
    }
}
