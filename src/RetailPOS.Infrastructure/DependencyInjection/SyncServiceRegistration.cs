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
        services.AddHttpClient<IProductSyncClient, HttpProductSyncClient>(client =>
        {
            client.BaseAddress = baseAddress;
        });

        services.AddScoped<ProductSyncService>();

        return services;
    }
}
