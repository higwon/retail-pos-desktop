using RetailPOS.Domain.Products;

namespace RetailPOS.Application.Persistence;

public interface IProductRepository
{
    Task<IReadOnlyList<Product>> GetActiveAsync(CancellationToken cancellationToken = default);
    Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Product?> GetByBarcodeAsync(string barcode, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Product>> SearchAsync(string keyword, CancellationToken cancellationToken = default);
}
