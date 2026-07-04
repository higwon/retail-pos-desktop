using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RetailPOS.Infrastructure.DependencyInjection;
using RetailPOS.Infrastructure.Persistence;

namespace RetailPOS.Infrastructure.Tests;

public sealed class LocalDatabaseInitializerTests : IAsyncLifetime
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "RetailPOS.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task InitializeAsync_CreatesDatabaseAndAppliesInitialMigration()
    {
        var databasePath = Path.Combine(_directory, "retail-pos.db");
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["LocalDatabase:DatabasePath"] = databasePath
            })
            .Build();
        var services = new ServiceCollection();
        services.AddLocalPersistence(configuration);

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var initializer = scope.ServiceProvider.GetRequiredService<LocalDatabaseInitializer>();

        await initializer.InitializeAsync();

        Assert.True(File.Exists(databasePath));
        var dbContext = scope.ServiceProvider.GetRequiredService<LocalPosDbContext>();
        var appliedMigrations = await dbContext.Database.GetAppliedMigrationsAsync();
        Assert.Contains(appliedMigrations, migration => migration.EndsWith("_InitialLocalDatabase"));
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }

        return Task.CompletedTask;
    }
}
