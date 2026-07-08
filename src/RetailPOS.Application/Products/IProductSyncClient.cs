namespace RetailPOS.Application.Products;

public interface IProductSyncClient
{
    Task<ProductSyncPage> GetProductsAsync(
        ProductSyncPageRequest request,
        CancellationToken cancellationToken = default);
}
