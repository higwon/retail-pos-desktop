namespace RetailPOS.Api.Orders;

public sealed record OrderUploadIdentity(
    Guid StoreId,
    Guid TerminalId,
    Guid LocalOrderId)
{
    public string ReferenceKey => $"{StoreId:N}:{TerminalId:N}:{LocalOrderId:N}";

    public static OrderUploadIdentity From(OrderUploadRequest request) =>
        new(request.StoreId, request.TerminalId, request.LocalOrderId);
}
