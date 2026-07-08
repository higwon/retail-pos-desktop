namespace RetailPOS.Api.Orders;

public sealed class OrderUploadConflictException : Exception
{
    private OrderUploadConflictException(string message)
        : base(message)
    {
    }

    public static OrderUploadConflictException ForIdempotencyKey(string idempotencyKey) =>
        new($"idempotencyKey already belongs to a different local order: {idempotencyKey}");

    public static OrderUploadConflictException ForOrderIdentity(string referenceKey) =>
        new($"order identity already belongs to a different idempotencyKey: {referenceKey}");
}
