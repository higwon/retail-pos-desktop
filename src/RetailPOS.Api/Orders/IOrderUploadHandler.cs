namespace RetailPOS.Api.Orders;

public interface IOrderUploadHandler
{
    Task<OrderUploadResponse> UploadAsync(
        OrderUploadRequest request,
        CancellationToken cancellationToken = default);
}
