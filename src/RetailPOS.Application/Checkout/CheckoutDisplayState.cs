using RetailPOS.Domain.Payments;

namespace RetailPOS.Application.Checkout;

public sealed class CheckoutDisplayState
{
    private CheckoutDisplaySnapshot _snapshot = CheckoutDisplaySnapshot.Cart();

    public event EventHandler? Changed;

    public CheckoutDisplaySnapshot Snapshot => _snapshot;

    public void ShowCart()
    {
        Set(CheckoutDisplaySnapshot.Cart());
    }

    public void ShowPaymentWaiting(PaymentMethod method, decimal amount)
    {
        Set(new CheckoutDisplaySnapshot(
            CheckoutDisplayPhase.PaymentWaiting,
            "Payment in progress",
            $"{method} payment waiting",
            amount));
    }

    public void ShowPaymentFailed(string message)
    {
        Set(new CheckoutDisplaySnapshot(
            CheckoutDisplayPhase.PaymentFailed,
            "Payment could not be completed",
            string.IsNullOrWhiteSpace(message) ? "Please try another payment method" : message.Trim(),
            null));
    }

    public void ShowCompleted()
    {
        Set(new CheckoutDisplaySnapshot(
            CheckoutDisplayPhase.Completed,
            "Thank you",
            "Payment complete. Please take your receipt.",
            null));
    }

    private void Set(CheckoutDisplaySnapshot snapshot)
    {
        _snapshot = snapshot;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}

public sealed record CheckoutDisplaySnapshot(
    CheckoutDisplayPhase Phase,
    string StatusMessage,
    string PaymentMessage,
    decimal? PaymentAmount)
{
    public static CheckoutDisplaySnapshot Cart() => new(
        CheckoutDisplayPhase.Cart,
        "Please check your items",
        "Waiting for payment",
        null);
}

public enum CheckoutDisplayPhase
{
    Cart,
    PaymentWaiting,
    PaymentFailed,
    Completed
}
