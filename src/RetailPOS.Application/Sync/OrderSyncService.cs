using System.Text.Json;
using System.Text.Json.Serialization;
using RetailPOS.Application.Orders;
using RetailPOS.Application.Persistence;

namespace RetailPOS.Application.Sync;

public sealed class OrderSyncService(
    ISyncQueueRepository syncQueueRepository,
    IOrderUploadClient orderUploadClient,
    IOrderSyncClock clock)
{
    public const int MaxAutomaticAttempts = 5;
    private const string OrderItemType = "Order";

    private static readonly TimeSpan[] RetryDelays =
    [
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(2),
        TimeSpan.FromMinutes(4),
        TimeSpan.FromMinutes(8),
        TimeSpan.FromMinutes(16)
    ];

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task<OrderSyncRunResult> ProcessDueAsync(
        DateTimeOffset asOfUtc,
        int count,
        CancellationToken cancellationToken = default)
    {
        if (asOfUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("asOfUtc must be a UTC timestamp.", nameof(asOfUtc));
        }

        if (count <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "count must be greater than zero.");
        }

        var dueItems = await syncQueueRepository.GetDuePendingAsync(asOfUtc, count, cancellationToken);
        var completed = 0;
        var retried = 0;
        var exhausted = 0;
        var skipped = 0;

        foreach (var item in dueItems)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!string.Equals(item.ItemType, OrderItemType, StringComparison.OrdinalIgnoreCase))
            {
                skipped++;
                continue;
            }

            if (item.RetryCount >= MaxAutomaticAttempts)
            {
                await MarkExhaustedAsync(item, item.RetryCount, "Automatic retry limit reached.", cancellationToken);
                exhausted++;
                continue;
            }

            try
            {
                var payload = DeserializePayload(item);
                await orderUploadClient.UploadAsync(payload, cancellationToken);
                await syncQueueRepository.MarkCompletedAsync(item.Id, UtcNow(), cancellationToken);
                completed++;
            }
            catch (OrderUploadConflictException exception)
            {
                await MarkExhaustedAsync(item, item.RetryCount, Summarize(exception), cancellationToken);
                exhausted++;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                var retryCount = item.RetryCount + 1;
                if (retryCount >= MaxAutomaticAttempts)
                {
                    await MarkExhaustedAsync(item, retryCount, Summarize(exception), cancellationToken);
                    exhausted++;
                    continue;
                }

                var nextAttemptAtUtc = asOfUtc + RetryDelays[Math.Min(retryCount, MaxAutomaticAttempts) - 1];
                await syncQueueRepository.UpdateRetryAsync(
                    item.Id,
                    retryCount,
                    nextAttemptAtUtc,
                    Summarize(exception),
                    UtcNow(),
                    cancellationToken);
                retried++;
            }
        }

        return new OrderSyncRunResult(dueItems.Count, completed, retried, exhausted, skipped);
    }

    private static OrderUploadPayload DeserializePayload(SyncQueueRecord item)
    {
        if (string.IsNullOrWhiteSpace(item.PayloadJson))
        {
            throw new InvalidOperationException("Order sync queue payload is empty.");
        }

        return JsonSerializer.Deserialize<OrderUploadPayload>(item.PayloadJson, JsonOptions)
            ?? throw new InvalidOperationException("Order sync queue payload could not be restored.");
    }

    private DateTimeOffset UtcNow()
    {
        var value = clock.UtcNow;
        if (value.Offset != TimeSpan.Zero)
        {
            throw new InvalidOperationException("Order sync clock must return UTC timestamps.");
        }

        return value;
    }

    private Task MarkExhaustedAsync(
        SyncQueueRecord item,
        int retryCount,
        string? lastErrorSummary,
        CancellationToken cancellationToken) =>
        syncQueueRepository.MarkExhaustedAsync(
            item.Id,
            retryCount,
            lastErrorSummary,
            UtcNow(),
            cancellationToken);

    private static string Summarize(Exception exception)
    {
        var message = string.IsNullOrWhiteSpace(exception.Message)
            ? exception.GetType().Name
            : exception.Message;

        return message.Length <= 200 ? message : message[..200];
    }
}

public sealed record OrderSyncRunResult(
    int ProcessedCount,
    int CompletedCount,
    int RetriedCount,
    int ExhaustedCount,
    int SkippedCount);
