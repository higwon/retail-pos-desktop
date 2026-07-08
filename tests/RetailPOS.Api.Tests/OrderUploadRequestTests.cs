using RetailPOS.Api.Orders;

namespace RetailPOS.Api.Tests;

public sealed class OrderUploadRequestTests
{
    [Fact]
    public void Validate_AcceptsCompletedLocalOrderContract()
    {
        var request = ValidRequest();

        var result = request.Validate();

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_RequiresCurrentSchemaVersion()
    {
        var request = ValidRequest() with { SchemaVersion = 2 };

        var result = request.Validate();

        Assert.False(result.Succeeded);
        Assert.Contains(nameof(OrderUploadRequest.SchemaVersion), result.Errors.Keys);
    }

    [Fact]
    public void Validate_RequiresUtcTimestamps()
    {
        var request = ValidRequest() with
        {
            CreatedAt = new DateTimeOffset(2026, 7, 8, 10, 0, 0, TimeSpan.FromHours(9)),
            Payments =
            [
                new OrderUploadPaymentRequest(
                    "Card",
                    9000m,
                    "APP-001",
                    "TX-001",
                    new DateTimeOffset(2026, 7, 8, 10, 0, 0, TimeSpan.FromHours(9)))
            ]
        };

        var result = request.Validate();

        Assert.False(result.Succeeded);
        Assert.Contains(nameof(OrderUploadRequest.CreatedAt), result.Errors.Keys);
        Assert.Contains("Payments[0].ApprovedAtUtc", result.Errors.Keys);
    }

    [Fact]
    public void Validate_RequiresLineTotalsToMatchOrderTotal()
    {
        var request = ValidRequest() with
        {
            Lines =
            [
                new OrderUploadLineRequest(
                    Guid.Parse("11111111-0000-0000-0000-000000000001"),
                    "Sample Product",
                    5000m,
                    2,
                    1000m,
                    8000m)
            ]
        };

        var result = request.Validate();

        Assert.False(result.Succeeded);
        Assert.Contains(nameof(OrderUploadRequest.Lines), result.Errors.Keys);
    }

    [Fact]
    public void Validate_RequiresPaymentTotalToMatchOrderTotal()
    {
        var request = ValidRequest() with
        {
            Payments =
            [
                new OrderUploadPaymentRequest(
                    "Card",
                    8000m,
                    "APP-001",
                    "TX-001",
                    new DateTimeOffset(2026, 7, 8, 1, 1, 0, TimeSpan.Zero))
            ]
        };

        var result = request.Validate();

        Assert.False(result.Succeeded);
        Assert.Contains(nameof(OrderUploadRequest.Payments), result.Errors.Keys);
    }

    [Fact]
    public async Task EmptyOrderUploadHandler_ReturnsRunnablePlaceholderResponse()
    {
        var request = ValidRequest();
        var handler = new EmptyOrderUploadHandler();

        var response = await handler.UploadAsync(request);

        Assert.Equal(request.LocalOrderId, response.ServerOrderId);
        Assert.Equal($"PENDING-{request.LocalOrderNumber}", response.OrderNumber);
        Assert.Equal("Accepted", response.SyncStatus);
    }

    private static OrderUploadRequest ValidRequest() => new(
        OrderUploadRequest.CurrentSchemaVersion,
        Guid.Parse("10000000-0000-0000-0000-000000000001"),
        Guid.Parse("20000000-0000-0000-0000-000000000001"),
        Guid.Parse("30000000-0000-0000-0000-000000000001"),
        "10000000000000000000000000000001:20000000000000000000000000000001:30000000000000000000000000000001",
        "POS-20260708-000001",
        new DateOnly(2026, 7, 8),
        Guid.Parse("40000000-0000-0000-0000-000000000001"),
        10000m,
        1000m,
        9000m,
        new DateTimeOffset(2026, 7, 8, 1, 0, 0, TimeSpan.Zero),
        [
            new OrderUploadLineRequest(
                Guid.Parse("11111111-0000-0000-0000-000000000001"),
                "Sample Product",
                5000m,
                2,
                1000m,
                9000m)
        ],
        [
            new OrderUploadPaymentRequest(
                "Card",
                9000m,
                "APP-001",
                "TX-001",
                new DateTimeOffset(2026, 7, 8, 1, 1, 0, TimeSpan.Zero))
        ]);
}
