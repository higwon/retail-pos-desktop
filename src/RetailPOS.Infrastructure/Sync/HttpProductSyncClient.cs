using System.Net.Http.Json;
using RetailPOS.Application.Products;

namespace RetailPOS.Infrastructure.Sync;

public sealed class HttpProductSyncClient(HttpClient httpClient) : IProductSyncClient
{
    public async Task<ProductSyncPage> GetProductsAsync(
        ProductSyncPageRequest request,
        CancellationToken cancellationToken = default)
    {
        var query = BuildQuery(request);
        var response = await httpClient.GetFromJsonAsync<ProductSyncResponseDto>(
            $"api/products{query}",
            cancellationToken);

        if (response is null)
        {
            throw new InvalidOperationException("Product sync API returned an empty response.");
        }

        return new ProductSyncPage(
            response.Products.Select(product => new ProductSyncItem(
                product.Id,
                product.Sku,
                product.Barcode,
                product.Name,
                product.CategoryName,
                product.UnitPrice,
                product.StockQuantity,
                product.IsActive,
                product.Version,
                product.UpdatedUtc)).ToList(),
            response.Page,
            response.PageSize,
            response.HasMore,
            response.ServerTimeUtc);
    }

    private static string BuildQuery(ProductSyncPageRequest request)
    {
        var parameters = new List<string>
        {
            $"page={request.Page}",
            $"pageSize={request.PageSize}"
        };

        if (request.UpdatedAfter is not null)
        {
            parameters.Add($"updatedAfter={Uri.EscapeDataString(request.UpdatedAfter.Value.ToString("O"))}");
        }

        return $"?{string.Join("&", parameters)}";
    }

    private sealed record ProductSyncResponseDto(
        IReadOnlyList<ProductSyncProductDto> Products,
        int Page,
        int PageSize,
        bool HasMore,
        DateTimeOffset ServerTimeUtc);

    private sealed record ProductSyncProductDto(
        Guid Id,
        string Sku,
        string Barcode,
        string Name,
        string CategoryName,
        decimal UnitPrice,
        int StockQuantity,
        bool IsActive,
        long Version,
        DateTimeOffset UpdatedUtc);
}
