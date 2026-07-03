using RetailPOS.Domain.Common;

namespace RetailPOS.Domain.Discounts;

public sealed class ManualDiscount
{
    private ManualDiscount(DiscountType type, decimal value)
    {
        Type = type;
        Value = value;
    }

    public DiscountType Type { get; }
    public decimal Value { get; }

    public static ManualDiscount FixedAmount(decimal amount) =>
        new(DiscountType.FixedAmount, DomainGuard.Money(amount, nameof(amount)));

    public static ManualDiscount Percentage(decimal rate)
    {
        if (rate < 0 || rate > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(rate), "Discount rate must be between 0 and 100.");
        }

        return new ManualDiscount(DiscountType.Percentage, rate);
    }

    public decimal CalculateAmount(decimal subtotal)
    {
        DomainGuard.Money(subtotal, nameof(subtotal));
        var calculated = Type == DiscountType.FixedAmount
            ? Value
            : decimal.Round(subtotal * Value / 100m, 0, MidpointRounding.AwayFromZero);

        return decimal.Min(subtotal, calculated);
    }
}
