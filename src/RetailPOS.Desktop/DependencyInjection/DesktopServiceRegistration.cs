using Microsoft.Extensions.DependencyInjection;
using RetailPOS.Application.Checkout;
using RetailPOS.Application.Payments;
using RetailPOS.Desktop.Controls;
using RetailPOS.Desktop.Diagnostics;
using RetailPOS.Desktop.ViewModels;
using RetailPOS.Desktop.Views;

namespace RetailPOS.Desktop.DependencyInjection;

public static class DesktopServiceRegistration
{
    public static IServiceCollection AddDesktopServices(this IServiceCollection services)
    {
        services.AddScoped<MainWindow>();
        services.AddSingleton<GlobalExceptionHandler>();
        services.AddScoped<NavigationHost>();
        services.AddScoped<CheckoutSession>();
        services.AddSingleton<ICheckoutContextProvider, DemoCheckoutContextProvider>();
        services.AddSingleton<ICheckoutClock, SystemCheckoutClock>();
        services.AddSingleton<ICheckoutIdGenerator, GuidCheckoutIdGenerator>();
        services.AddSingleton<IPaymentSimulator, LocalPaymentSimulator>();
        services.AddScoped<IRecoverablePaymentStartService, RecoverablePaymentStartService>();
        services.AddScoped<IOrderCompletionService, OrderCompletionService>();
        services.AddTransient<LoginView>();
        services.AddTransient<PosMainView>();
        services.AddTransient<ProductGridView>();
        services.AddTransient<CartPanelView>();
        services.AddTransient<CustomerDisplayWindow>();
        services.AddTransient<PaymentDialog>();
        services.AddTransient<ReceiptDialog>();
        services.AddTransient<CheckoutRecoveryView>();
        services.AddTransient<DashboardView>();
        services.AddTransient<StatusView>();
        services.AddTransient<LoginViewModel>();
        services.AddTransient<PosMainViewModel>();
        services.AddTransient<ProductGridViewModel>();
        services.AddTransient<CartPanelViewModel>();
        services.AddTransient<CustomerDisplayViewModel>();
        services.AddTransient<PaymentDialogViewModel>();
        services.AddTransient<ReceiptViewModel>();
        services.AddTransient<CheckoutRecoveryViewModel>();
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<StatusViewModel>();
        services.AddTransient<Func<CustomerDisplayWindow>>(provider =>
            () => provider.GetRequiredService<CustomerDisplayWindow>());
        services.AddTransient<Func<PaymentDialog>>(provider =>
            () => provider.GetRequiredService<PaymentDialog>());
        services.AddTransient<Func<ReceiptDialog>>(provider =>
            () => provider.GetRequiredService<ReceiptDialog>());

        return services;
    }
}
