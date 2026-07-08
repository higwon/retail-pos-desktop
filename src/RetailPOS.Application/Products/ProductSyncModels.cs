namespace RetailPOS.Application.Products;

public sealed record ProductSyncPageRequest(
    DateTimeOffset? UpdatedAfter,
    int Page,
    int PageSize);

public sealed record ProductSyncPage(
    IReadOnlyList<ProductSyncItem> Products,
    int Page,
    int PageSize,
    bool HasMore,
    DateTimeOffset ServerTimeUtc);

public sealed record ProductSyncItem(
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

public sealed record ProductSyncResult(
    int UpsertedCount,
    DateTimeOffset ServerTimeUtc);
