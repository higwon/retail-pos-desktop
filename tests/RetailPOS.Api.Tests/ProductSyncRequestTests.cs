using RetailPOS.Api.Products;

namespace RetailPOS.Api.Tests;

public sealed class ProductSyncRequestTests
{
    [Fact]
    public void Create_AcceptsUtcIncrementalSyncRequest()
    {
        var updatedAfter = new DateTimeOffset(2026, 7, 8, 0, 0, 0, TimeSpan.Zero);

        var result = ProductSyncRequest.Create(updatedAfter, page: 2, pageSize: 250);

        Assert.True(result.Succeeded);
        Assert.Equal(updatedAfter, result.Value.UpdatedAfter);
        Assert.Equal(2, result.Value.Page);
        Assert.Equal(250, result.Value.PageSize);
    }

    [Fact]
    public void Create_RejectsNonUtcUpdatedAfter()
    {
        var updatedAfter = new DateTimeOffset(2026, 7, 8, 9, 0, 0, TimeSpan.FromHours(9));

        var result = ProductSyncRequest.Create(updatedAfter, page: 1, pageSize: 100);

        Assert.False(result.Succeeded);
        Assert.Contains("updatedAfter", result.Errors.Keys);
    }

    [Theory]
    [InlineData(0, 100, "page")]
    [InlineData(1, 0, "pageSize")]
    [InlineData(1, 501, "pageSize")]
    public void Create_RejectsInvalidPaging(int page, int pageSize, string errorKey)
    {
        var result = ProductSyncRequest.Create(null, page, pageSize);

        Assert.False(result.Succeeded);
        Assert.Contains(errorKey, result.Errors.Keys);
    }
}
