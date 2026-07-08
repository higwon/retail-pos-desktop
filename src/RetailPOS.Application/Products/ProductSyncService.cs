namespace RetailPOS.Application.Products;

public sealed class ProductSyncService(
    IProductSyncClient syncClient,
    IProductSyncStore syncStore)
{
    public async Task<ProductSyncResult> SyncAsync(
        DateTimeOffset? updatedAfter,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        if (updatedAfter is { Offset: var offset } && offset != TimeSpan.Zero)
        {
            throw new ArgumentException("updatedAfter must be a UTC timestamp.", nameof(updatedAfter));
        }

        if (pageSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pageSize), "pageSize must be greater than zero.");
        }

        var page = 1;
        var upsertedCount = 0;
        var serverTimeUtc = DateTimeOffset.UtcNow;
        var hasMore = true;

        while (hasMore)
        {
            var response = await syncClient.GetProductsAsync(
                new ProductSyncPageRequest(updatedAfter, page, pageSize),
                cancellationToken);

            var responseServerTimeUtc = EnsureUtc(response.ServerTimeUtc, nameof(response.ServerTimeUtc));
            upsertedCount += await syncStore.UpsertAsync(response.Products, cancellationToken);
            serverTimeUtc = responseServerTimeUtc;
            hasMore = response.HasMore;
            page++;
        }

        return new ProductSyncResult(upsertedCount, serverTimeUtc);
    }

    private static DateTimeOffset EnsureUtc(DateTimeOffset value, string parameterName)
    {
        if (value.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Timestamp must use UTC.", parameterName);
        }

        return value;
    }
}
