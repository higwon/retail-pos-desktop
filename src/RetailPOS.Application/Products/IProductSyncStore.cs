namespace RetailPOS.Application.Products;

public interface IProductSyncStore
{
    Task<int> UpsertAsync(
        IReadOnlyList<ProductSyncItem> products,
        CancellationToken cancellationToken = default);
}
