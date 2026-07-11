using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RetailPOS.Application.Checkout;
using RetailPOS.Application.Persistence;
using RetailPOS.Domain.Payments;
using RetailPOS.Infrastructure.DependencyInjection;
using RetailPOS.Infrastructure.Persistence;

namespace RetailPOS.Infrastructure.Tests;

public sealed class RestartRecoveryIntegrationTests
{
    private static readonly DateTimeOffset NowUtc = new(2026, 7, 11, 2, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task InterruptedPayment_BecomesDurableUnknownOnceAcrossRepeatedRestarts()
    {
        var directory = Path.Combine(Path.GetTempPath(), "RetailPOS.Restart.Tests", Guid.NewGuid().ToString("N"));
        var databasePath = Path.Combine(directory, "retail-pos.db");
        try
        {
            await using (var first = await CreateProviderAsync(databasePath))
            await using (var scope = first.CreateAsyncScope())
            {
                await scope.ServiceProvider.GetRequiredService<IPendingCheckoutRepository>()
                    .SaveAsync(InterruptedCheckout());
            }

            await using (var restarted = await CreateProviderAsync(databasePath))
            await using (var scope = restarted.CreateAsyncScope())
            {
                var recovery = scope.ServiceProvider.GetRequiredService<ICheckoutRecoveryService>();
                var firstLoad = Assert.Single(await recovery.GetRecoverableAsync());
                var repeatedLoad = Assert.Single(await recovery.GetRecoverableAsync());
                Assert.Equal(PendingCheckoutStatus.ManagerReviewRequired, firstLoad.RecoveryStatus);
                Assert.Equal("Unknown", firstLoad.PaymentMethod);
                Assert.Equal(firstLoad.PendingCheckoutId, repeatedLoad.PendingCheckoutId);
            }

            await using (var secondRestart = await CreateProviderAsync(databasePath))
            await using (var scope = secondRestart.CreateAsyncScope())
            {
                var record = Assert.Single(await scope.ServiceProvider
                    .GetRequiredService<IPendingCheckoutRepository>().GetUnresolvedAsync());
                Assert.Equal(PendingCheckoutStatus.ManagerReviewRequired, record.RecoveryStatus);
                Assert.Equal(PaymentStatus.Unknown, record.PaymentStatus);
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    private static async Task<ServiceProvider> CreateProviderAsync(string databasePath)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["LocalDatabase:DatabasePath"] = databasePath }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLocalPersistence(configuration);
        services.AddSingleton<ICheckoutClock>(new Clock());
        services.AddScoped<IOrderCompletionService, UnusedOrderCompletionService>();
        services.AddScoped<ICheckoutRecoveryService, CheckoutRecoveryService>();
        var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        await using var scope = provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<LocalDatabaseInitializer>().InitializeAsync();
        return provider;
    }

    private static PendingCheckoutRecord InterruptedCheckout() => new(
        Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001"),
        Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), NowUtc,
        PendingCheckoutStatus.AwaitingPayment,
        "{\"lines\":[{\"productId\":\"11111111-0000-0000-0000-000000000001\",\"productName\":\"Cleanser\",\"unitPrice\":12000,\"quantity\":1,\"lineTotal\":12000}],\"subtotal\":12000,\"discountAmount\":0,\"total\":12000}",
        "{}", PaymentStatus.Unknown, null, null, null, null, null, null, NowUtc);

    private sealed class Clock : ICheckoutClock { public DateTimeOffset UtcNow => NowUtc; }
    private sealed class UnusedOrderCompletionService : IOrderCompletionService
    {
        public Task<OrderCompletionResult> CompleteAsync(Guid pendingCheckoutId, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Order completion is not used by this scenario.");
    }
}
