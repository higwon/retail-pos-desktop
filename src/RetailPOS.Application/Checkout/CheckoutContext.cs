namespace RetailPOS.Application.Checkout;

public sealed record CheckoutContext(Guid StoreId, Guid TerminalId, Guid CashierId);

public interface ICheckoutContextProvider
{
    CheckoutContext GetCurrent();
}

public sealed class DemoCheckoutContextProvider : ICheckoutContextProvider
{
    public static readonly Guid DemoStoreId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    public static readonly Guid DemoTerminalId = Guid.Parse("20000000-0000-0000-0000-000000000001");
    public static readonly Guid DemoCashierId = Guid.Parse("30000000-0000-0000-0000-000000000001");

    public CheckoutContext GetCurrent() => new(DemoStoreId, DemoTerminalId, DemoCashierId);
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
