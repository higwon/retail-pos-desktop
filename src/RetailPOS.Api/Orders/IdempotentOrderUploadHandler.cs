namespace RetailPOS.Api.Orders;

public sealed class IdempotentOrderUploadHandler(
    IOrderUploadIdempotencyStore idempotencyStore) : IOrderUploadHandler
{
    public Task<OrderUploadResponse> UploadAsync(
        OrderUploadRequest request,
        CancellationToken cancellationToken = default) =>
        idempotencyStore.GetOrCreateAsync(
            request,
            CreateResponse,
            cancellationToken);

    private static OrderUploadResponse CreateResponse(OrderUploadRequest request) =>
        new(
            Guid.NewGuid(),
            $"SYNCED-{request.LocalOrderNumber}",
            "Synced");
}
