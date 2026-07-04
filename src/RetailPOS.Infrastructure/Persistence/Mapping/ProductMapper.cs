using RetailPOS.Domain.Products;
using RetailPOS.Infrastructure.Persistence.Entities;

namespace RetailPOS.Infrastructure.Persistence.Mapping;

internal static class ProductMapper
{
    public static Product ToDomain(this ProductEntity entity) => new(
        entity.Id,
        entity.Sku,
        entity.Barcode,
        entity.Name,
        entity.CategoryName,
        entity.UnitPrice,
        entity.IsActive);
}
