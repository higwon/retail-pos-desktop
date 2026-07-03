using RetailPOS.Domain.Products;

namespace RetailPOS.Domain.Tests;

public sealed class ProductTests
{
    [Fact]
    public void Constructor_CreatesSearchFriendlyProduct()
    {
        var id = Guid.NewGuid();

        var product = new Product(id, " SKU-01 ", " 880000000001 ", " Shampoo ", " Beauty ", 5_000m);

        Assert.Equal(id, product.Id);
        Assert.Equal("SKU-01", product.Sku);
        Assert.Equal("880000000001", product.Barcode);
        Assert.Equal(5_000m, product.UnitPrice);
        Assert.True(product.IsActive);
    }

    [Fact]
    public void Constructor_RejectsFractionalKrwAmount() =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new Product(Guid.NewGuid(), "SKU", "BAR", "Name", "Category", 100.5m));
}
