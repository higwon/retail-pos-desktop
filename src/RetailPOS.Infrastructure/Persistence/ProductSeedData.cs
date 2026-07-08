using Microsoft.EntityFrameworkCore;
using RetailPOS.Infrastructure.Persistence.Entities;

namespace RetailPOS.Infrastructure.Persistence;

public sealed class ProductSeedData(LocalPosDbContext dbContext)
{
    private static readonly DateTime SeedUpdatedUtc = new(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);

    private static readonly ProductEntity[] Products =
    [
        Create("10000000-0000-0000-0000-000000000001", "DRINK-001", "8801000000011", "Mineral Water 500ml", "Beverages", 1000m),
        Create("10000000-0000-0000-0000-000000000002", "DRINK-002", "8801000000028", "Cola 355ml", "Beverages", 1800m),
        Create("10000000-0000-0000-0000-000000000003", "FOOD-001", "8801000000035", "Triangle Kimbap", "Ready Meals", 1500m),
        Create("10000000-0000-0000-0000-000000000004", "SNACK-001", "8801000000042", "Potato Chips", "Snacks", 2200m),
        Create("10000000-0000-0000-0000-000000000005", "DAILY-001", "8801000000059", "Pocket Tissues", "Daily Goods", 1200m),
        Create("10000000-0000-0000-0000-000000000006", "FOOD-002", "8801000000066", "Cup Noodles", "Ready Meals", 1900m)
    ];

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var seedIds = Products.Select(product => product.Id).ToArray();
        var existingIds = await dbContext.Products
            .Where(product => seedIds.Contains(product.Id))
            .Select(product => product.Id)
            .ToListAsync(cancellationToken);
        var missingProducts = Products
            .Where(product => !existingIds.Contains(product.Id))
            .Select(Clone)
            .ToList();

        if (missingProducts.Count > 0)
        {
            dbContext.Products.AddRange(missingProducts);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
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
