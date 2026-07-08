namespace RetailPOS.Api.Orders;

public sealed class EmptyOrderUploadHandler : IOrderUploadHandler
{
    public Task<OrderUploadResponse> UploadAsync(
        OrderUploadRequest request,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new OrderUploadResponse(
            request.LocalOrderId,
            $"PENDING-{request.LocalOrderNumber}",
            "Accepted"));
}
