namespace RetailPOS.Application.Orders;

public interface IOrderUploadClient
{
    Task<OrderUploadResult> UploadAsync(
        OrderUploadPayload payload,
        CancellationToken cancellationToken = default);
}

public sealed record OrderUploadResult(
    Guid ServerOrderId,
    string OrderNumber,
    string SyncStatus);
