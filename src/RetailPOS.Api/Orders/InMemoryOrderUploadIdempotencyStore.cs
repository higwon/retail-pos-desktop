namespace RetailPOS.Api.Orders;

public sealed class InMemoryOrderUploadIdempotencyStore : IOrderUploadIdempotencyStore
{
    private readonly object _gate = new();
    private readonly Dictionary<OrderUploadIdentity, StoredOrderUpload> _ordersByIdentity = new();
    private readonly Dictionary<string, OrderUploadIdentity> _identityByIdempotencyKey =
        new(StringComparer.Ordinal);

    public Task<OrderUploadResponse> GetOrCreateAsync(
        OrderUploadRequest request,
        Func<OrderUploadRequest, OrderUploadResponse> createResponse,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var identity = OrderUploadIdentity.From(request);
        var idempotencyKey = request.IdempotencyKey.Trim();

        lock (_gate)
        {
            if (_identityByIdempotencyKey.TryGetValue(idempotencyKey, out var existingIdentity)
                && existingIdentity != identity)
            {
                throw OrderUploadConflictException.ForIdempotencyKey(idempotencyKey);
            }

            if (_ordersByIdentity.TryGetValue(identity, out var existingOrder))
            {
                if (!string.Equals(existingOrder.IdempotencyKey, idempotencyKey, StringComparison.Ordinal))
                {
                    throw OrderUploadConflictException.ForOrderIdentity(identity.ReferenceKey);
                }

                return Task.FromResult(existingOrder.Response);
            }

            var response = createResponse(request);
            _identityByIdempotencyKey.Add(idempotencyKey, identity);
            _ordersByIdentity.Add(identity, new StoredOrderUpload(idempotencyKey, response));

            return Task.FromResult(response);
        }
    }

    private sealed record StoredOrderUpload(
        string IdempotencyKey,
        OrderUploadResponse Response);
}
