using RetailPOS.Application.Checkout;

namespace RetailPOS.Application.Authentication;

public sealed record CashierSession(
    Guid StoreId,
    Guid TerminalId,
    Guid CashierId,
    string EmployeeCode,
    string CashierName,
    DateTimeOffset StartedAtUtc)
{
    public CheckoutContext ToCheckoutContext() => new(StoreId, TerminalId, CashierId);
}
