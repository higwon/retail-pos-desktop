using Microsoft.Extensions.DependencyInjection;
using CommunityToolkit.Mvvm.Messaging;
using RetailPOS.Application.Authentication;
using RetailPOS.Application.Devices;
using RetailPOS.Application.Checkout;
using RetailPOS.Application.Payments;
using RetailPOS.Application.Receipts;
using RetailPOS.Desktop.Controls;
using RetailPOS.Desktop.Diagnostics;
using RetailPOS.Desktop.DeviceSimulation;
using RetailPOS.Desktop.ViewModels;
using RetailPOS.Desktop.Views;
using RetailPOS.Infrastructure.Devices;
using RetailPOS.Desktop.Workflow;

namespace RetailPOS.Desktop.DependencyInjection;

public static class DesktopServiceRegistration
{
    public static IServiceCollection AddDesktopServices(this IServiceCollection services)
    {
        services.AddScoped<MainWindow>();
        services.AddSingleton<IMessenger>(WeakReferenceMessenger.Default);
        services.AddSingleton<GlobalExceptionHandler>();
        services.AddScoped<NavigationHost>();
        services.AddScoped<CheckoutSession>();
        services.AddScoped<CheckoutDisplayState>();
        services.AddScoped<ReceiptPreviewState>();
        services.AddScoped<CurrentSessionContext>();
        services.AddScoped<ICurrentSessionContext>(provider =>
            provider.GetRequiredService<CurrentSessionContext>());
        services.AddScoped<ICheckoutContextProvider>(provider =>
            provider.GetRequiredService<CurrentSessionContext>());
        services.AddScoped<ILoginService, DemoLoginService>();
        services.AddSingleton<ICheckoutClock, SystemCheckoutClock>();
        services.AddSingleton<ICheckoutIdGenerator, GuidCheckoutIdGenerator>();
        services.AddSingleton<IReceiptContextProvider, DemoReceiptContextProvider>();
        services.AddSingleton<SimulatedReceiptPrinter>();
        services.AddSingleton<IReceiptPrinter>(provider =>
            provider.GetRequiredService<SimulatedReceiptPrinter>());
        services.AddSingleton<IReceiptPrinterSimulatorControl>(provider =>
            provider.GetRequiredService<SimulatedReceiptPrinter>());
        services.AddSingleton<SimulatedBarcodeScanner>();
        services.AddSingleton<IBarcodeScanner>(provider =>
            provider.GetRequiredService<SimulatedBarcodeScanner>());
        services.AddSingleton<IBarcodeScannerSimulatorControl>(provider =>
            provider.GetRequiredService<SimulatedBarcodeScanner>());
        services.AddSingleton<SimulatedPaymentTerminal>();
        services.AddSingleton<IPaymentTerminal>(provider =>
            provider.GetRequiredService<SimulatedPaymentTerminal>());
        services.AddSingleton<IPaymentTerminalSimulatorControl>(provider =>
            provider.GetRequiredService<SimulatedPaymentTerminal>());
        services.AddSingleton<ICashPaymentProcessor, LocalCashPaymentProcessor>();
        services.AddScoped<IRecoverablePaymentStartService, RecoverablePaymentStartService>();
        services.AddScoped<IOrderCompletionService, OrderCompletionService>();
        services.AddScoped<ICheckoutRecoveryService, CheckoutRecoveryService>();
        services.AddScoped<IReceiptService, ReceiptService>();
        services.AddTransient<LoginView>();
        services.AddTransient<PosMainView>();
        services.AddTransient<ProductGridView>();
        services.AddTransient<CartPanelView>();
        services.AddTransient<CustomerDisplayWindow>();
        services.AddTransient<PaymentDialog>();
        services.AddTransient<ReceiptDialog>();
        services.AddTransient<DeviceSimulatorWindow>();
        services.AddTransient<CheckoutRecoveryView>();
        services.AddTransient<DashboardView>();
        services.AddTransient<StatusView>();
        services.AddTransient<LoginViewModel>();
        services.AddTransient<PosMainViewModel>();
        services.AddScoped<ProductGridViewModel>();
        services.AddTransient<CartPanelViewModel>();
        services.AddTransient<CustomerDisplayViewModel>();
        services.AddTransient<PaymentDialogViewModel>();
        services.AddTransient<ReceiptViewModel>();
        services.AddTransient<DeviceSimulatorViewModel>();
        services.AddTransient<ReceiptPrinterSimulatorViewModel>();
        services.AddTransient<BarcodeScannerSimulatorViewModel>();
        services.AddTransient<CardTerminalSimulatorViewModel>();
        services.AddTransient<CustomerDisplayHostViewModel>();
        services.AddTransient<CheckoutRecoveryViewModel>();
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<StatusViewModel>();
        services.AddTransient<Func<CustomerDisplayWindow>>(provider =>
            () => provider.GetRequiredService<CustomerDisplayWindow>());
        services.AddSingleton<IDisplayTargetProvider, WindowsDisplayTargetProvider>();
        services.AddSingleton<IDisplayTopologyMonitor, WindowsDisplayTopologyMonitor>();
        services.AddTransient<Func<ICustomerDisplayWindow>>(provider =>
            () => provider.GetRequiredService<CustomerDisplayWindow>());
        services.AddScoped<CustomerDisplayHost>();
        services.AddTransient<Func<PaymentDialog>>(provider =>
            () => provider.GetRequiredService<PaymentDialog>());
        services.AddTransient<Func<ReceiptDialog>>(provider =>
            () => provider.GetRequiredService<ReceiptDialog>());
        services.AddScoped<WorkflowWindowHost<PaymentDialog>>();
        services.AddScoped<WorkflowWindowHost<ReceiptDialog>>();
        services.AddTransient<Func<DeviceSimulatorWindow>>(provider =>
            () => provider.GetRequiredService<DeviceSimulatorWindow>());
        services.AddScoped<DeviceSimulatorWindowHost>();
        services.AddScoped<DeviceStatusService>();
        services.AddScoped<BarcodeScannerCoordinator>();
        services.AddScoped<IUiDispatcher>(_ => new WpfUiDispatcher(
            System.Windows.Application.Current.Dispatcher));

        return services;
    }
}
