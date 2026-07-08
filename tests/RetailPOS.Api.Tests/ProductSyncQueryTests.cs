using RetailPOS.Api.Products;

namespace RetailPOS.Api.Tests;

public sealed class ProductSyncQueryTests
{
    [Fact]
    public async Task EmptyProductSyncQuery_ReturnsContractShapeWithPaging()
    {
        var request = new ProductSyncRequest(null, Page: 3, PageSize: 50);
        var query = new EmptyProductSyncQuery();

        var response = await query.GetProductsAsync(request);

        Assert.Empty(response.Products);
        Assert.Equal(3, response.Page);
        Assert.Equal(50, response.PageSize);
        Assert.False(response.HasMore);
        Assert.Equal(TimeSpan.Zero, response.ServerTimeUtc.Offset);
    }

    [Fact]
    public void ProductSyncProductDto_IncludesIncrementalSyncAndSoftDeleteFields()
    {
        var updatedUtc = new DateTimeOffset(2026, 7, 8, 0, 0, 0, TimeSpan.Zero);
        var product = new ProductSyncProductDto(
            Guid.Parse("11111111-0000-0000-0000-000000000001"),
            "SKU-001",
            "880000000001",
            "Cola",
            "Drink",
            1800m,
            StockQuantity: 10,
            IsActive: false,
            Version: 12,
            updatedUtc);

        Assert.False(product.IsActive);
        Assert.Equal(12, product.Version);
        Assert.Equal(updatedUtc, product.UpdatedUtc);
    }
}
