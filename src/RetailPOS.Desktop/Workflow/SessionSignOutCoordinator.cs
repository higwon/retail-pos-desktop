using RetailPOS.Application.Authentication;
using RetailPOS.Application.Checkout;
using RetailPOS.Desktop.DeviceSimulation;
using RetailPOS.Desktop.ViewModels;
using RetailPOS.Desktop.Views;

namespace RetailPOS.Desktop.Workflow;

public interface ISessionWorkflowLifecycle
{
    void StopScanner();
    void ClosePayment();
    void CloseReceipt();
    void CloseCustomerDisplay();
}

public sealed class SessionWorkflowLifecycle(
    BarcodeScannerCoordinator scannerCoordinator,
    WorkflowWindowHost<PaymentDialog> paymentHost,
    WorkflowWindowHost<ReceiptDialog> receiptHost,
    CustomerDisplayHost customerDisplayHost) : ISessionWorkflowLifecycle
{
    public void StopScanner() => scannerCoordinator.Stop();
    public void ClosePayment() => paymentHost.Close();
    public void CloseReceipt() => receiptHost.Close();
    public void CloseCustomerDisplay() => customerDisplayHost.Close();
}

public sealed class SessionSignOutCoordinator(
    ISessionWorkflowLifecycle workflowLifecycle,
    CheckoutSession checkoutSession,
    ReceiptPreviewState receiptPreviewState,
    ICurrentSessionContext sessionContext)
{
    public void SignOut()
    {
        receiptPreviewState.Clear();
        workflowLifecycle.ClosePayment();
        workflowLifecycle.CloseReceipt();
        workflowLifecycle.CloseCustomerDisplay();
        workflowLifecycle.StopScanner();
        checkoutSession.Clear();
        sessionContext.Clear();
    }
}
