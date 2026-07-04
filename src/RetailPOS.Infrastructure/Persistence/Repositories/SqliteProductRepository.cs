using Microsoft.EntityFrameworkCore;
using RetailPOS.Application.Persistence;
using RetailPOS.Domain.Products;
using RetailPOS.Infrastructure.Persistence.Mapping;

namespace RetailPOS.Infrastructure.Persistence.Repositories;

public sealed class SqliteProductRepository(LocalPosDbContext dbContext) : IProductRepository
{
    public async Task<IReadOnlyList<Product>> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        var entities = await dbContext.Products
            .AsNoTracking()
            .Where(product => product.IsActive)
            .OrderBy(product => product.Name)
            .ThenBy(product => product.Id)
            .ToListAsync(cancellationToken);
        return entities.Select(product => product.ToDomain()).ToList();
    }

    public async Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.Products.AsNoTracking()
            .SingleOrDefaultAsync(product => product.Id == id, cancellationToken);
        return entity?.ToDomain();
    }

    public async Task<Product?> GetByBarcodeAsync(string barcode, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(barcode);
        var normalizedBarcode = barcode.Trim();
        var entity = await dbContext.Products.AsNoTracking()
            .SingleOrDefaultAsync(
                product => product.IsActive && product.Barcode == normalizedBarcode,
                cancellationToken);
        return entity?.ToDomain();
    }

    public async Task<IReadOnlyList<Product>> SearchAsync(
        string keyword,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(keyword);
        var pattern = $"%{keyword.Trim()}%";
        var entities = await dbContext.Products.AsNoTracking()
            .Where(product => product.IsActive &&
                (EF.Functions.Like(product.Name, pattern) ||
                 EF.Functions.Like(product.Sku, pattern) ||
                 EF.Functions.Like(product.Barcode, pattern)))
            .OrderBy(product => product.Name)
            .ThenBy(product => product.Id)
            .ToListAsync(cancellationToken);
        return entities.Select(product => product.ToDomain()).ToList();
    }
}
