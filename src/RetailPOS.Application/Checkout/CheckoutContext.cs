namespace RetailPOS.Application.Checkout;

public sealed record CheckoutContext(Guid StoreId, Guid TerminalId, Guid CashierId);

public interface ICheckoutContextProvider
{
    CheckoutContext GetCurrent();
}

public interface ICheckoutClock
{
    DateTimeOffset UtcNow { get; }
}

public sealed class SystemCheckoutClock : ICheckoutClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}

public interface ICheckoutIdGenerator
{
    Guid NewId();
}

public sealed class GuidCheckoutIdGenerator : ICheckoutIdGenerator
{
    public Guid NewId() => Guid.NewGuid();
}
