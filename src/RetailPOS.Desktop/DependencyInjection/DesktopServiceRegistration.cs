using Microsoft.Extensions.DependencyInjection;

namespace RetailPOS.Desktop.DependencyInjection;

public static class DesktopServiceRegistration
{
    public static IServiceCollection AddDesktopServices(this IServiceCollection services)
    {
        services.AddSingleton<MainWindow>();

        return services;
    }
}
