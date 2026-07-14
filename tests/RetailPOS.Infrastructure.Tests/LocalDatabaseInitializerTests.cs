using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
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

    [Fact]
    public async Task InitializeAsync_UpgradesExistingDatabaseWithoutLosingPendingCheckout()
    {
        Directory.CreateDirectory(_directory);
        var databasePath = Path.Combine(_directory, "upgrade.db");
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
        var dbContext = scope.ServiceProvider.GetRequiredService<LocalPosDbContext>();
        await dbContext.GetService<IMigrator>()
            .MigrateAsync("20260711150346_AddDashboardQueryIndex");
        var checkoutId = Guid.NewGuid();
        var now = new DateTime(2026, 7, 14, 1, 0, 0, DateTimeKind.Utc);
        await dbContext.Database.ExecuteSqlInterpolatedAsync($$"""
            INSERT INTO PendingCheckouts
                (Id, StoreId, TerminalId, CashierId, CreatedAtUtc, RecoveryStatus,
                 CartSnapshotJson, PaymentSnapshotJson, PaymentStatus, ApprovalCode,
                 ApprovedAmount, TransactionReference, PaymentApprovedAtUtc, OrderId,
                 CompletedAtUtc, LastUpdatedAtUtc)
            VALUES
                ({{checkoutId}}, {{Guid.NewGuid()}}, {{Guid.NewGuid()}}, {{Guid.NewGuid()}}, {{now}}, 2,
                 {{"{}"}}, {{"{}"}}, 1, {{"APP-LEGACY"}}, {{5000m}}, {{"TX-LEGACY"}},
                 {{now}}, NULL, NULL, {{now}})
            """);

        await scope.ServiceProvider.GetRequiredService<LocalDatabaseInitializer>()
            .InitializeAsync();

        var restored = await dbContext.PendingCheckouts.AsNoTracking()
            .SingleAsync(checkout => checkout.Id == checkoutId);
        Assert.Equal("APP-LEGACY", restored.ApprovalCode);
        Assert.Equal(5000m, restored.ApprovedAmount);
        Assert.Null(restored.CashTenderedAmount);
        Assert.Null(restored.ChangeAmount);
        var appliedMigrations = await dbContext.Database.GetAppliedMigrationsAsync();
        Assert.Contains(appliedMigrations, migration => migration.EndsWith("_PersistCashTenderMetadata"));
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
