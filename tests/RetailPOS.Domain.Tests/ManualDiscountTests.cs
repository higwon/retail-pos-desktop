using RetailPOS.Domain.Discounts;

namespace RetailPOS.Domain.Tests;

public sealed class ManualDiscountTests
{
    [Theory]
    [InlineData(10_000, 1_500, 1_500)]
    [InlineData(1_000, 5_000, 1_000)]
    [InlineData(10_000, 0, 0)]
    public void FixedAmount_CapsDiscountAtSubtotal(decimal subtotal, decimal value, decimal expected) =>
        Assert.Equal(expected, ManualDiscount.FixedAmount(value).CalculateAmount(subtotal));

    [Theory]
    [InlineData(10_000, 10, 1_000)]
    [InlineData(9_999, 12.5, 1_250)]
    [InlineData(10_000, 100, 10_000)]
    public void Percentage_RoundsToWholeWon(decimal subtotal, decimal rate, decimal expected) =>
        Assert.Equal(expected, ManualDiscount.Percentage(rate).CalculateAmount(subtotal));

    [Fact]
    public void Percentage_RejectsRateOverOneHundred() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => ManualDiscount.Percentage(100.01m));
}
