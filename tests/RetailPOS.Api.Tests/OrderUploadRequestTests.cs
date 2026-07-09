using RetailPOS.Api.Orders;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

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
    public void Validate_RequiresTimestampValues()
    {
        var request = ValidRequest() with
        {
            CreatedAt = default,
            Payments =
            [
                new OrderUploadPaymentRequest(
                    "Card",
                    9000m,
                    "APP-001",
                    "TX-001",
                    default)
            ]
        };

        var result = request.Validate();

        Assert.False(result.Succeeded);
        Assert.Equal("createdAt is required.", Assert.Single(result.Errors[nameof(OrderUploadRequest.CreatedAt)]));
        Assert.Equal("approvedAtUtc is required.", Assert.Single(result.Errors["Payments[0].ApprovedAtUtc"]));
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
    public async Task IdempotentOrderUploadHandler_ReturnsSyncedResponse()
    {
        var request = ValidRequest();
        var handler = Handler();

        var response = await handler.UploadAsync(request);

        Assert.NotEqual(Guid.Empty, response.ServerOrderId);
        Assert.Equal($"SYNCED-{request.LocalOrderNumber}", response.OrderNumber);
        Assert.Equal("Synced", response.SyncStatus);
    }

    [Fact]
    public async Task IdempotentOrderUploadHandler_ReturnsExistingResponseForDuplicateOrder()
    {
        var request = ValidRequest();
        var handler = Handler();

        var firstResponse = await handler.UploadAsync(request);
        var duplicateResponse = await handler.UploadAsync(request);

        Assert.Equal(firstResponse, duplicateResponse);
    }

    [Fact]
    public async Task IdempotentOrderUploadHandler_RejectsReusedIdempotencyKeyForDifferentOrder()
    {
        var request = ValidRequest();
        var conflictingRequest = request with
        {
            LocalOrderId = Guid.Parse("30000000-0000-0000-0000-000000000002"),
            LocalOrderNumber = "POS-20260708-000002"
        };
        var handler = Handler();

        await handler.UploadAsync(request);

        await Assert.ThrowsAsync<OrderUploadConflictException>(() => handler.UploadAsync(conflictingRequest));
    }

    [Fact]
    public async Task IdempotentOrderUploadHandler_RejectsDifferentIdempotencyKeyForExistingOrder()
    {
        var request = ValidRequest();
        var conflictingRequest = request with
        {
            IdempotencyKey = "different-idempotency-key"
        };
        var handler = Handler();

        await handler.UploadAsync(request);

        await Assert.ThrowsAsync<OrderUploadConflictException>(() => handler.UploadAsync(conflictingRequest));
    }

    [Fact]
    public async Task IdempotentOrderUploadHandler_ConcurrentSameOrderWithDifferentKeys_AllowsOneUpload()
    {
        var request = ValidRequest();
        var conflictingRequest = request with
        {
            IdempotencyKey = "different-idempotency-key"
        };
        var handler = Handler();
        using var start = new ManualResetEventSlim();

        var firstUpload = Task.Run(() => UploadAfterStartAsync(handler, request, start));
        var secondUpload = Task.Run(() => UploadAfterStartAsync(handler, conflictingRequest, start));

        start.Set();
        var results = await Task.WhenAll(firstUpload, secondUpload);

        Assert.Single(results, result => result.Exception is null);
        Assert.Single(results, result => result.Exception is OrderUploadConflictException);
    }

    [Fact]
    public async Task IdempotentOrderUploadHandler_ConcurrentSameKeyWithDifferentOrders_AllowsOneUpload()
    {
        var request = ValidRequest();
        var conflictingRequest = request with
        {
            LocalOrderId = Guid.Parse("30000000-0000-0000-0000-000000000002"),
            LocalOrderNumber = "POS-20260708-000002"
        };
        var handler = Handler();
        using var start = new ManualResetEventSlim();

        var firstUpload = Task.Run(() => UploadAfterStartAsync(handler, request, start));
        var secondUpload = Task.Run(() => UploadAfterStartAsync(handler, conflictingRequest, start));

        start.Set();
        var results = await Task.WhenAll(firstUpload, secondUpload);

        Assert.Single(results, result => result.Exception is null);
        Assert.Single(results, result => result.Exception is OrderUploadConflictException);
    }

    [Fact]
    public async Task IdempotentOrderUploadHandler_ConflictLogDoesNotIncludeIdempotencyKey()
    {
        var request = ValidRequest();
        var logger = new RecordingLogger<IdempotentOrderUploadHandler>();
        var handler = new IdempotentOrderUploadHandler(new InMemoryOrderUploadIdempotencyStore(), logger);
        var conflictingRequest = request with
        {
            IdempotencyKey = "secret-idempotency-key"
        };

        await handler.UploadAsync(request);
        await Assert.ThrowsAsync<OrderUploadConflictException>(() => handler.UploadAsync(conflictingRequest));

        Assert.Contains(logger.Messages, message => message.Contains("idempotency conflict", StringComparison.OrdinalIgnoreCase));
        Assert.All(logger.Messages, message =>
        {
            Assert.DoesNotContain(request.IdempotencyKey, message, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("secret-idempotency-key", message, StringComparison.OrdinalIgnoreCase);
        });
    }

    private static async Task<OrderUploadAttempt> UploadAfterStartAsync(
        IOrderUploadHandler handler,
        OrderUploadRequest request,
        ManualResetEventSlim start)
    {
        start.Wait();

        try
        {
            var response = await handler.UploadAsync(request);
            return new OrderUploadAttempt(response, null);
        }
        catch (Exception exception)
        {
            return new OrderUploadAttempt(null, exception);
        }
    }

    private static IdempotentOrderUploadHandler Handler() =>
        new(
            new InMemoryOrderUploadIdempotencyStore(),
            NullLogger<IdempotentOrderUploadHandler>.Instance);

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

    private sealed record OrderUploadAttempt(
        OrderUploadResponse? Response,
        Exception? Exception);

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception));
        }
    }
}
