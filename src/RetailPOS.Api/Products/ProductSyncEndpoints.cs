namespace RetailPOS.Api.Products;

public static class ProductSyncEndpoints
{
    private const int DefaultPage = 1;
    private const int DefaultPageSize = 100;

    public static RouteGroupBuilder MapProductSyncEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/products", async (
                DateTimeOffset? updatedAfter,
                int? page,
                int? pageSize,
                IProductSyncQuery query,
                CancellationToken cancellationToken) =>
            {
                var requestResult = ProductSyncRequest.Create(
                    updatedAfter,
                    page ?? DefaultPage,
                    pageSize ?? DefaultPageSize);

                if (!requestResult.Succeeded)
                {
                    return Results.ValidationProblem(requestResult.Errors);
                }

                var response = await query.GetProductsAsync(
                    requestResult.Value,
                    cancellationToken);

                return Results.Ok(response);
            })
            .WithName("GetProductSync")
            .Produces<ProductSyncResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem();

        return api;
    }
}
