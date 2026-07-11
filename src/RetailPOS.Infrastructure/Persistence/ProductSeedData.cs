using Microsoft.EntityFrameworkCore;
using RetailPOS.Infrastructure.Persistence.Entities;

namespace RetailPOS.Infrastructure.Persistence;

public sealed class ProductSeedData(LocalPosDbContext dbContext)
{
    private static readonly DateTime SeedUpdatedUtc = new(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);

    private static readonly ProductEntity[] Products =
    [
        Create("10000000-0000-0000-0000-000000000001", "SKIN-001", "8801000000011", "Low pH Facial Cleanser", "Skin Care", 12000m),
        Create("10000000-0000-0000-0000-000000000002", "SUN-001", "8801000000028", "Daily Moisture Sunscreen SPF50+", "Sun Care", 18000m),
        Create("10000000-0000-0000-0000-000000000003", "SKIN-002", "8801000000035", "Hydrating Sheet Mask", "Skin Care", 2500m),
        Create("10000000-0000-0000-0000-000000000004", "HAIR-001", "8801000000042", "Damage Repair Shampoo", "Hair Care", 15000m),
        Create("10000000-0000-0000-0000-000000000005", "MAKEUP-001", "8801000000059", "Soft Velvet Lip Tint", "Makeup", 13000m),
        Create("10000000-0000-0000-0000-000000000006", "HEALTH-001", "8801000000066", "Vitamin C Gummies", "Health", 16000m)
    ];

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var seedIds = Products.Select(product => product.Id).ToArray();
        var existingProducts = await dbContext.Products
            .Where(product => seedIds.Contains(product.Id))
            .ToListAsync(cancellationToken);
        var existingIds = existingProducts.Select(product => product.Id).ToHashSet();
        var missingProducts = Products
            .Where(product => !existingIds.Contains(product.Id))
            .Select(Clone)
            .ToList();

        foreach (var existing in existingProducts.Where(product => product.Version == 0))
        {
            var seed = Products.Single(product => product.Id == existing.Id);
            existing.Sku = seed.Sku;
            existing.Barcode = seed.Barcode;
            existing.Name = seed.Name;
            existing.CategoryName = seed.CategoryName;
            existing.UnitPrice = seed.UnitPrice;
            existing.IsActive = seed.IsActive;
            existing.UpdatedUtc = seed.UpdatedUtc;
        }

        if (missingProducts.Count > 0)
        {
            dbContext.Products.AddRange(missingProducts);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static ProductEntity Create(
        string id,
        string sku,
        string barcode,
        string name,
        string categoryName,
        decimal unitPrice) => new()
        {
            Id = Guid.Parse(id),
            Sku = sku,
            Barcode = barcode,
            Name = name,
            CategoryName = categoryName,
            UnitPrice = unitPrice,
            StockQuantity = 0,
            IsActive = true,
            Version = 0,
            UpdatedUtc = SeedUpdatedUtc
        };

    private static ProductEntity Clone(ProductEntity product) => new()
    {
        Id = product.Id,
        Sku = product.Sku,
        Barcode = product.Barcode,
        Name = product.Name,
    CategoryName = product.CategoryName,
    UnitPrice = product.UnitPrice,
    StockQuantity = product.StockQuantity,
    IsActive = product.IsActive,
    Version = product.Version,
    UpdatedUtc = product.UpdatedUtc
};
}
