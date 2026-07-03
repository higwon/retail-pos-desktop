using Microsoft.Extensions.DependencyInjection;
using RetailPOS.Desktop.Controls;
using RetailPOS.Desktop.ViewModels;
using RetailPOS.Desktop.Views;

namespace RetailPOS.Desktop.DependencyInjection;

public static class DesktopServiceRegistration
{
    public static IServiceCollection AddDesktopServices(this IServiceCollection services)
    {
        services.AddSingleton<MainWindow>();
        services.AddSingleton<NavigationHost>();
        services.AddTransient<LoginView>();
        services.AddTransient<PosMainView>();
        services.AddTransient<ProductGridView>();
        services.AddTransient<CartPanelView>();
        services.AddTransient<CustomerDisplayWindow>();
        services.AddTransient<LoginViewModel>();
        services.AddTransient<PosMainViewModel>();
        services.AddTransient<ProductGridViewModel>();
        services.AddTransient<CartPanelViewModel>();
        services.AddTransient<CustomerDisplayViewModel>();
        services.AddTransient<Func<CustomerDisplayWindow>>(provider =>
            () => provider.GetRequiredService<CustomerDisplayWindow>());

        return services;
    }
}
