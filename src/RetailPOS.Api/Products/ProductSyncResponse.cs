namespace RetailPOS.Api.Products;

public sealed record ProductSyncResponse(
    IReadOnlyList<ProductSyncProductDto> Products,
    int Page,
    int PageSize,
    bool HasMore,
    DateTimeOffset ServerTimeUtc);

public sealed record ProductSyncProductDto(
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
