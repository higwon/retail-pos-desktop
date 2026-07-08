namespace RetailPOS.Api.Products;

public sealed class EmptyProductSyncQuery : IProductSyncQuery
{
    public Task<ProductSyncResponse> GetProductsAsync(
        ProductSyncRequest request,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new ProductSyncResponse(
            [],
            request.Page,
            request.PageSize,
            HasMore: false,
            DateTimeOffset.UtcNow));
}
