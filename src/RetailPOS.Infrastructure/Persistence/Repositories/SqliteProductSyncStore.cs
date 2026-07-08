using Microsoft.EntityFrameworkCore;
using RetailPOS.Application.Products;
using RetailPOS.Infrastructure.Persistence.Entities;

namespace RetailPOS.Infrastructure.Persistence.Repositories;

public sealed class SqliteProductSyncStore(LocalPosDbContext dbContext) : IProductSyncStore
{
    public async Task<int> UpsertAsync(
        IReadOnlyList<ProductSyncItem> products,
        CancellationToken cancellationToken = default)
    {
        var changedCount = 0;

        foreach (var product in products)
        {
            Validate(product);
            var entity = await dbContext.Products
                .SingleOrDefaultAsync(item => item.Id == product.Id, cancellationToken);

            if (entity is null)
            {
                dbContext.Products.Add(ToEntity(product));
                changedCount++;
                continue;
            }

            if (product.Version < entity.Version)
            {
                continue;
            }

            Apply(entity, product);
            changedCount++;
        }

        if (changedCount > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return changedCount;
    }

    private static ProductEntity ToEntity(ProductSyncItem product) => new()
    {
        Id = product.Id,
        Sku = product.Sku.Trim(),
        Barcode = product.Barcode.Trim(),
        Name = product.Name.Trim(),
        CategoryName = product.CategoryName.Trim(),
        UnitPrice = product.UnitPrice,
        StockQuantity = product.StockQuantity,
        IsActive = product.IsActive,
        Version = product.Version,
        UpdatedUtc = product.UpdatedUtc.UtcDateTime
    };

    private static void Apply(ProductEntity entity, ProductSyncItem product)
    {
        entity.Sku = product.Sku.Trim();
        entity.Barcode = product.Barcode.Trim();
        entity.Name = product.Name.Trim();
        entity.CategoryName = product.CategoryName.Trim();
        entity.UnitPrice = product.UnitPrice;
        entity.StockQuantity = product.StockQuantity;
        entity.IsActive = product.IsActive;
        entity.Version = product.Version;
        entity.UpdatedUtc = product.UpdatedUtc.UtcDateTime;
    }

    private static void Validate(ProductSyncItem product)
    {
        if (product.Id == Guid.Empty)
        {
            throw new ArgumentException("Product identity is required.", nameof(product));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(product.Sku);
        ArgumentException.ThrowIfNullOrWhiteSpace(product.Barcode);
        ArgumentException.ThrowIfNullOrWhiteSpace(product.Name);
        ArgumentException.ThrowIfNullOrWhiteSpace(product.CategoryName);

        if (product.UnitPrice < 0m || product.UnitPrice != decimal.Truncate(product.UnitPrice))
        {
            throw new ArgumentException("Product unit price must be a whole non-negative amount.", nameof(product));
        }

        if (product.StockQuantity < 0)
        {
            throw new ArgumentException("Product stock quantity must be non-negative.", nameof(product));
        }

        if (product.Version < 0)
        {
            throw new ArgumentException("Product version must be non-negative.", nameof(product));
        }

        if (product.UpdatedUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Product updated timestamp must use UTC.", nameof(product));
        }
    }
}
