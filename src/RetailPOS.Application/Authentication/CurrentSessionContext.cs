using RetailPOS.Application.Checkout;

namespace RetailPOS.Application.Authentication;

public interface ICurrentSessionContext
{
    CashierSession? Current { get; }
    bool IsSignedIn { get; }
    void SignIn(CashierSession session);
    void Clear();
}

public sealed class CurrentSessionContext : ICurrentSessionContext, ICheckoutContextProvider
{
    public CashierSession? Current { get; private set; }
    public bool IsSignedIn => Current is not null;

    public void SignIn(CashierSession session) => Current = session;

    public void Clear() => Current = null;

    public CheckoutContext GetCurrent()
    {
        if (Current is null)
        {
            throw new InvalidOperationException("No cashier session is active.");
        }

        return Current.ToCheckoutContext();
    }
}
