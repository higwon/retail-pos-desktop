using RetailPOS.Application.Payments;
using RetailPOS.Domain.Payments;

namespace RetailPOS.Application.Tests;

public sealed class LocalCashPaymentProcessorTests
{
    [Fact]
    public async Task AcceptAsync_ApprovesWholeKrwAmount()
    {
        var now = new DateTimeOffset(2026, 7, 10, 1, 2, 3, TimeSpan.Zero);
        var processor = new LocalCashPaymentProcessor(new StubTimeProvider(now));

        var result = await processor.AcceptAsync(new CashPaymentRequest(Guid.NewGuid(), 3600m));

        Assert.Equal(PaymentStatus.Approved, result.Status);
        Assert.Equal(3600m, result.ApprovedAmount);
        Assert.Equal(now, result.ApprovedAtUtc);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1.5)]
    public async Task AcceptAsync_RejectsInvalidAmount(decimal amount)
    {
        var processor = new LocalCashPaymentProcessor(TimeProvider.System);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            processor.AcceptAsync(new CashPaymentRequest(Guid.NewGuid(), amount)));
    }

    private sealed class StubTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
