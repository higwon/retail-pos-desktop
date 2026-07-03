using RetailPOS.Domain.Orders;
using RetailPOS.Domain.Payments;

namespace RetailPOS.Domain.Tests;

public sealed class OrderAndPaymentTests
{
    [Fact]
    public void Order_CalculatesTotalsFromLineSnapshots()
    {
        var lines = new[]
        {
            new OrderLine(Guid.NewGuid(), "Shampoo", 5_000m, 2, 1_000m),
            new OrderLine(Guid.NewGuid(), "Drink", 2_000m, 1)
        };

        var order = new Order(Guid.NewGuid(), "POS-20260704-000001", Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), new DateOnly(2026, 7, 4), DateTimeOffset.UtcNow, lines);

        Assert.Equal(12_000m, order.SubtotalAmount);
        Assert.Equal(1_000m, order.DiscountAmount);
        Assert.Equal(11_000m, order.TotalAmount);
    }

    [Fact]
    public void Order_RejectsNonUtcTimestamp()
    {
        var line = new OrderLine(Guid.NewGuid(), "Product", 1_000m, 1);
        var nonUtc = new DateTimeOffset(2026, 7, 4, 12, 0, 0, TimeSpan.FromHours(9));

        Assert.Throws<ArgumentException>(() => new Order(Guid.NewGuid(), "ORDER", Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), new DateOnly(2026, 7, 4), nonUtc, [line]));
    }

    [Fact]
    public void Payment_ApprovesOnceAndCapturesReferenceData()
    {
        var payment = new Payment(Guid.NewGuid(), PaymentMethod.Card, 9_000m, DateTimeOffset.UtcNow);
        var approvedAt = DateTimeOffset.UtcNow;

        payment.Approve(9_000m, approvedAt, "APPROVED-001", "TX-001");

        Assert.Equal(PaymentStatus.Approved, payment.Status);
        Assert.Equal(9_000m, payment.ApprovedAmount);
        Assert.Equal("APPROVED-001", payment.ApprovalCode);
        Assert.Throws<InvalidOperationException>(() => payment.Cancel());
    }

    [Fact]
    public void Payment_FailureRequiresReason()
    {
        var payment = new Payment(Guid.NewGuid(), PaymentMethod.Card, 9_000m, DateTimeOffset.UtcNow);

        Assert.Throws<ArgumentException>(() => payment.Fail(" "));
        Assert.Equal(PaymentStatus.Pending, payment.Status);
    }

    [Fact]
    public void Payment_InvalidApprovalDoesNotPartiallyChangeState()
    {
        var payment = new Payment(Guid.NewGuid(), PaymentMethod.Card, 9_000m, DateTimeOffset.UtcNow);
        var nonUtc = new DateTimeOffset(2026, 7, 4, 12, 0, 0, TimeSpan.FromHours(9));

        Assert.Throws<ArgumentException>(() => payment.Approve(9_000m, nonUtc, "APPROVED-001"));
        Assert.Equal(PaymentStatus.Pending, payment.Status);
        Assert.Null(payment.ApprovedAmount);
        Assert.Null(payment.ApprovedAtUtc);
        Assert.Null(payment.ApprovalCode);
    }
}
