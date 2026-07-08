using RetailPOS.Application.Checkout;
using RetailPOS.Application.Persistence;
using RetailPOS.Application.Receipts;
using RetailPOS.Domain.Orders;
using RetailPOS.Domain.Payments;

namespace RetailPOS.Application.Tests;

public sealed class ReceiptServiceTests
{
    private static readonly Guid OrderId = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001");
    private static readonly DateTimeOffset CreatedAtUtc = new(2026, 7, 8, 1, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset IssuedAtUtc = new(2026, 7, 8, 1, 1, 0, TimeSpan.Zero);

    [Fact]
    public async Task GenerateAsync_BuildsReceiptFromCompletedOrder()
    {
        var service = Service(CompletedOrder());

        var receipt = await service.GenerateAsync(OrderId);

        Assert.Equal("LOCAL-20260708-0001", receipt.OrderNumber);
        Assert.Equal(new DateOnly(2026, 7, 8), receipt.BusinessDate);
        Assert.Equal(3600m, receipt.SubtotalAmount);
        Assert.Equal(200m, receipt.DiscountAmount);
        Assert.Equal(3400m, receipt.TotalAmount);
        Assert.Equal(3400m, Assert.Single(receipt.Payments).Amount);
        Assert.Equal("APP-001", receipt.Payments[0].ApprovalCode);
        Assert.Contains("LOCAL-20260708-0001", receipt.PlainText);
    }

    [Fact]
    public async Task GenerateAsync_RejectsMissingOrder()
    {
        var service = Service(null);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.GenerateAsync(OrderId));
    }

    [Fact]
    public async Task PrintAsync_ReturnsSimulationResultWithoutDeviceDependency()
    {
        var service = Service(CompletedOrder());
        var receipt = await service.GenerateAsync(OrderId);

        var result = await service.PrintAsync(receipt);

        Assert.True(result.Succeeded);
        Assert.Equal(IssuedAtUtc, result.PrintedAtUtc);
        Assert.Equal("Receipt print simulated.", result.Message);
    }

    private static ReceiptService Service(Order? order) => new(
        new StubOrderRepository(order),
        new StubReceiptContextProvider(),
        new StubCheckoutClock(IssuedAtUtc));

    private static Order CompletedOrder()
    {
        var payment = new Payment(Guid.NewGuid(), PaymentMethod.Card, 3400m, CreatedAtUtc);
        payment.Approve(3400m, CreatedAtUtc.AddSeconds(5), "APP-001", "TX-001");
        return new Order(
            OrderId,
            "LOCAL-20260708-0001",
            Guid.Parse("10000000-0000-0000-0000-000000000001"),
            Guid.Parse("20000000-0000-0000-0000-000000000001"),
            Guid.Parse("30000000-0000-0000-0000-000000000001"),
            new DateOnly(2026, 7, 8),
            CreatedAtUtc,
            [new OrderLine(Guid.NewGuid(), "Cola", 1800m, 2, 200m)],
            [payment]);
    }

    private sealed class StubOrderRepository(Order? order) : IOrderRepository
    {
        public Task SaveAsync(Order order, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<Order?> GetByIdAsync(Guid localOrderId, CancellationToken cancellationToken = default) =>
            Task.FromResult(order?.LocalOrderId == localOrderId ? order : null);

        public Task<Order?> GetByNumberAsync(string localOrderNumber, CancellationToken cancellationToken = default) =>
            Task.FromResult(order?.LocalOrderNumber == localOrderNumber ? order : null);

        public Task<IReadOnlyList<Order>> GetRecentAsync(int count, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Order>>(order is null ? [] : [order]);

        public Task<bool> ExistsAsync(Guid localOrderId, CancellationToken cancellationToken = default) =>
            Task.FromResult(order?.LocalOrderId == localOrderId);
    }

    private sealed class StubReceiptContextProvider : IReceiptContextProvider
    {
        public ReceiptContext GetCurrent(Order order) => new(
            "Retail Store",
            "Local POS Terminal",
            "Cashier A",
            "Register 01");
    }

    private sealed class StubCheckoutClock(DateTimeOffset utcNow) : ICheckoutClock
    {
        public DateTimeOffset UtcNow => utcNow;
    }
}
