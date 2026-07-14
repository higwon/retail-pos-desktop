using RetailPOS.Application.Persistence;
using RetailPOS.Application.Receipts;
using RetailPOS.Domain.Orders;
using RetailPOS.Domain.Payments;

namespace RetailPOS.Application.Tests;

public sealed class ReceiptHistoryQueryTests
{
    private static readonly Guid OrderId = Guid.Parse("74000000-0000-0000-0000-000000000001");
    private static readonly Guid CashierId = Guid.Parse("74000000-0000-0000-0000-000000000002");
    private static readonly Guid TerminalId = Guid.Parse("74000000-0000-0000-0000-000000000003");
    private static readonly DateTimeOffset CompletedAtUtc =
        new(2026, 7, 14, 1, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task SearchAsync_NormalizesSearchAndMapsCashierContext()
    {
        var repository = new StubReceiptHistoryRepository(new ReceiptHistoryPageRecord(
            [new ReceiptHistorySummaryRecord(
                OrderId,
                "LOCAL-20260714-001",
                CompletedAtUtc,
                PaymentMethod.Cash,
                3400m,
                CashierId,
                TerminalId)],
            HasMore: true));
        var query = new ReceiptHistoryQuery(
            repository,
            new StubReceiptService(null),
            new StubReceiptContextProvider());

        var result = await query.SearchAsync(new ReceiptHistoryRequest(
            new DateOnly(2026, 7, 14),
            "  LOCAL-001  ",
            Offset: 10,
            Limit: 25));

        Assert.Equal("LOCAL-001", repository.SearchText);
        Assert.Equal(10, repository.Offset);
        Assert.Equal(25, repository.Limit);
        Assert.True(result.HasMore);
        var summary = Assert.Single(result.Items);
        Assert.Equal(OrderId, summary.LocalOrderId);
        Assert.Equal("Cashier A", summary.CashierName);
        Assert.Equal(PaymentMethod.Cash, summary.PaymentMethod);
    }

    [Theory]
    [InlineData(-1, 10)]
    [InlineData(0, 0)]
    [InlineData(0, 101)]
    public async Task SearchAsync_RejectsUnboundedRequests(int offset, int limit)
    {
        var query = new ReceiptHistoryQuery(
            new StubReceiptHistoryRepository(new([], false)),
            new StubReceiptService(null),
            new StubReceiptContextProvider());

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => query.SearchAsync(
            new ReceiptHistoryRequest(new DateOnly(2026, 7, 14), Offset: offset, Limit: limit)));
    }

    [Fact]
    public async Task GetDetailAsync_ReturnsNullWhenOrderNoLongerExists()
    {
        var query = new ReceiptHistoryQuery(
            new StubReceiptHistoryRepository(new([], false)),
            new StubReceiptService(null, throwMissing: true),
            new StubReceiptContextProvider());

        var detail = await query.GetDetailAsync(OrderId);

        Assert.Null(detail);
    }

    private sealed class StubReceiptHistoryRepository(ReceiptHistoryPageRecord page)
        : IReceiptHistoryRepository
    {
        public string? SearchText { get; private set; }
        public int Offset { get; private set; }
        public int Limit { get; private set; }

        public Task<ReceiptHistoryPageRecord> SearchAsync(
            DateOnly businessDate,
            string? searchText,
            int offset,
            int limit,
            CancellationToken cancellationToken = default)
        {
            SearchText = searchText;
            Offset = offset;
            Limit = limit;
            return Task.FromResult(page);
        }
    }

    private sealed class StubReceiptService(ReceiptPreview? preview, bool throwMissing = false)
        : IReceiptService
    {
        public Task<ReceiptPreview> GenerateAsync(
            Guid localOrderId,
            CancellationToken cancellationToken = default) =>
            throwMissing
                ? throw new KeyNotFoundException()
                : Task.FromResult(preview!);
    }

    private sealed class StubReceiptContextProvider : IReceiptContextProvider
    {
        public ReceiptContext GetCurrent(Guid cashierId, Guid terminalId) =>
            new("Store", "Address", "Cashier A", "Register 01");
    }
}
