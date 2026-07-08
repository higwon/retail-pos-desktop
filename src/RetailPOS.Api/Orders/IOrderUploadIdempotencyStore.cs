namespace RetailPOS.Api.Orders;

public interface IOrderUploadIdempotencyStore
{
    Task<OrderUploadResponse> GetOrCreateAsync(
        OrderUploadRequest request,
        Func<OrderUploadRequest, OrderUploadResponse> createResponse,
        CancellationToken cancellationToken = default);
}
