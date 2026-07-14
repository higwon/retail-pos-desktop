using RetailPOS.Application.Authentication;
using RetailPOS.Application.Checkout;
using RetailPOS.Desktop.DeviceSimulation;
using RetailPOS.Desktop.ViewModels;

namespace RetailPOS.Desktop.Workflow;

public interface ISessionWorkflowLifecycle
{
    void StopScanner();
    void CancelPayment();
    void ResetReceiptWorkflow();
    void CloseSimulator();
    void CloseCustomerDisplay();
}

public sealed class SessionWorkflowLifecycle(
    BarcodeScannerCoordinator scannerCoordinator,
    ICheckoutPaymentCoordinator paymentCoordinator,
    ReceiptHistoryViewModel receiptHistoryViewModel,
    DeviceSimulatorWindowHost deviceSimulatorWindowHost,
    CustomerDisplayHost customerDisplayHost) : ISessionWorkflowLifecycle
{
    public void StopScanner() => scannerCoordinator.Stop();
    public void CancelPayment() => paymentCoordinator.CancelActivePayment();
    public void ResetReceiptWorkflow() => receiptHistoryViewModel.ResetSession();
    public void CloseSimulator() => deviceSimulatorWindowHost.Close();
    public void CloseCustomerDisplay() => customerDisplayHost.Close();
}

public sealed class SessionSignOutCoordinator(
    ISessionWorkflowLifecycle workflowLifecycle,
    CheckoutSession checkoutSession,
    ReceiptPreviewState receiptPreviewState,
    ICurrentSessionContext sessionContext,
    CashierWorkflowNavigator workflowNavigator)
{
    public void SignOut()
    {
        receiptPreviewState.Clear();
        workflowLifecycle.CancelPayment();
        workflowLifecycle.ResetReceiptWorkflow();
        workflowLifecycle.CloseSimulator();
        workflowLifecycle.CloseCustomerDisplay();
        workflowLifecycle.StopScanner();
        checkoutSession.Clear();
        sessionContext.Clear();
        workflowNavigator.Reset(CashierWorkflowScreen.Login);
    }
}
