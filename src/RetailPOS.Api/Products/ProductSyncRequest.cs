namespace RetailPOS.Api.Products;

public sealed record ProductSyncRequest(
    DateTimeOffset? UpdatedAfter,
    int Page,
    int PageSize)
{
    public const int MaxPageSize = 500;

    public static ProductSyncRequestResult Create(
        DateTimeOffset? updatedAfter,
        int page,
        int pageSize)
    {
        var errors = new Dictionary<string, string[]>();

        if (updatedAfter is { Offset: var offset } && offset != TimeSpan.Zero)
        {
            errors[nameof(updatedAfter)] = ["updatedAfter must be a UTC timestamp."];
        }

        if (page < 1)
        {
            errors[nameof(page)] = ["page must be greater than or equal to 1."];
        }

        if (pageSize is < 1 or > MaxPageSize)
        {
            errors[nameof(pageSize)] = [$"pageSize must be between 1 and {MaxPageSize}."];
        }

        return errors.Count == 0
            ? ProductSyncRequestResult.Success(new ProductSyncRequest(updatedAfter, page, pageSize))
            : ProductSyncRequestResult.Failure(errors);
    }
}

public sealed class ProductSyncRequestResult
{
    private ProductSyncRequestResult(
        ProductSyncRequest? value,
        IDictionary<string, string[]> errors)
    {
        Value = value ?? new ProductSyncRequest(null, 1, 100);
        Errors = errors;
    }

    public bool Succeeded => Errors.Count == 0;
    public ProductSyncRequest Value { get; }
    public IDictionary<string, string[]> Errors { get; }

    public static ProductSyncRequestResult Success(ProductSyncRequest value) => new(
        value,
        new Dictionary<string, string[]>());

    public static ProductSyncRequestResult Failure(IDictionary<string, string[]> errors) => new(
        null,
        errors);
}
