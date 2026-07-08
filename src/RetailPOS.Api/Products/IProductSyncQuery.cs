namespace RetailPOS.Api.Products;

public interface IProductSyncQuery
{
    Task<ProductSyncResponse> GetProductsAsync(
        ProductSyncRequest request,
        CancellationToken cancellationToken = default);
}
