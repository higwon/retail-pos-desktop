namespace RetailPOS.Api.Orders;

public sealed record OrderUploadResponse(
    Guid ServerOrderId,
    string OrderNumber,
    string SyncStatus);
