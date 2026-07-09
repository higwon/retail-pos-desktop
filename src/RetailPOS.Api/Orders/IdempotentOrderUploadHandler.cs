namespace RetailPOS.Api.Orders;

using Microsoft.Extensions.Logging;

public sealed class IdempotentOrderUploadHandler(
    IOrderUploadIdempotencyStore idempotencyStore,
    ILogger<IdempotentOrderUploadHandler> logger) : IOrderUploadHandler
{
    public async Task<OrderUploadResponse> UploadAsync(
        OrderUploadRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await idempotencyStore.GetOrCreateAsync(
                request,
                CreateResponse,
                cancellationToken);

            logger.LogInformation(
                "Order upload accepted. {StoreId} {TerminalId} {LocalOrderId} {LocalOrderNumber} {ServerOrderId} {OrderNumber} {SyncStatus}",
                request.StoreId,
                request.TerminalId,
                request.LocalOrderId,
                request.LocalOrderNumber,
                response.ServerOrderId,
                response.OrderNumber,
                response.SyncStatus);

            return response;
        }
        catch (OrderUploadConflictException)
        {
            logger.LogWarning(
                "Order upload rejected because of an idempotency conflict. {StoreId} {TerminalId} {LocalOrderId} {LocalOrderNumber}",
                request.StoreId,
                request.TerminalId,
                request.LocalOrderId,
                request.LocalOrderNumber);
            throw;
        }
    }

    private static OrderUploadResponse CreateResponse(OrderUploadRequest request) =>
        new(
            Guid.NewGuid(),
            $"SYNCED-{request.LocalOrderNumber}",
            "Synced");
}
