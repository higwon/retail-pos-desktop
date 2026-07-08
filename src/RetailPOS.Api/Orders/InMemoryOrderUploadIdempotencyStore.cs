using System.Collections.Concurrent;

namespace RetailPOS.Api.Orders;

public sealed class InMemoryOrderUploadIdempotencyStore : IOrderUploadIdempotencyStore
{
    private readonly ConcurrentDictionary<OrderUploadIdentity, StoredOrderUpload> _ordersByIdentity = new();
    private readonly ConcurrentDictionary<string, OrderUploadIdentity> _identityByIdempotencyKey =
        new(StringComparer.Ordinal);

    public Task<OrderUploadResponse> GetOrCreateAsync(
        OrderUploadRequest request,
        Func<OrderUploadRequest, OrderUploadResponse> createResponse,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var identity = OrderUploadIdentity.From(request);
        var idempotencyKey = request.IdempotencyKey.Trim();

        if (_identityByIdempotencyKey.TryGetValue(idempotencyKey, out var existingIdentity)
            && existingIdentity != identity)
        {
            throw OrderUploadConflictException.ForIdempotencyKey(idempotencyKey);
        }

        while (true)
        {
            if (_ordersByIdentity.TryGetValue(identity, out var existingOrder))
            {
                if (!string.Equals(existingOrder.IdempotencyKey, idempotencyKey, StringComparison.Ordinal))
                {
                    throw OrderUploadConflictException.ForOrderIdentity(identity.ReferenceKey);
                }

                return Task.FromResult(existingOrder.Response);
            }

            if (!_identityByIdempotencyKey.TryAdd(idempotencyKey, identity))
            {
                continue;
            }

            var response = createResponse(request);
            var storedOrder = new StoredOrderUpload(idempotencyKey, response);
            if (_ordersByIdentity.TryAdd(identity, storedOrder))
            {
                return Task.FromResult(response);
            }

            _identityByIdempotencyKey.TryRemove(
                new KeyValuePair<string, OrderUploadIdentity>(idempotencyKey, identity));
        }
    }

    private sealed record StoredOrderUpload(
        string IdempotencyKey,
        OrderUploadResponse Response);
}
