using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using RetailPOS.Infrastructure.Configuration;

namespace RetailPOS.Infrastructure.Persistence;

public sealed class DesignTimeLocalPosDbContextFactory : IDesignTimeDbContextFactory<LocalPosDbContext>
{
    public LocalPosDbContext CreateDbContext(string[] args)
    {
        var databasePath = new LocalDatabaseOptions().DatabasePath;
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            ForeignKeys = true
        }.ToString();
        var options = new DbContextOptionsBuilder<LocalPosDbContext>()
            .UseSqlite(connectionString)
            .Options;

        return new LocalPosDbContext(options);
    }
}
