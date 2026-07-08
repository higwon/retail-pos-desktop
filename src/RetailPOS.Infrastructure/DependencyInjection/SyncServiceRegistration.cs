using Microsoft.Extensions.DependencyInjection;
using RetailPOS.Application.Products;
using RetailPOS.Infrastructure.Sync;

namespace RetailPOS.Infrastructure.DependencyInjection;

public static class SyncServiceRegistration
{
    public static IServiceCollection AddApiSyncClient(
        this IServiceCollection services,
        Uri baseAddress)
    {
        services.AddSingleton<IProductSyncClient>(
            new HttpProductSyncClient(new HttpClient { BaseAddress = baseAddress }));

        services.AddScoped<ProductSyncService>();

        return services;
    }
}
