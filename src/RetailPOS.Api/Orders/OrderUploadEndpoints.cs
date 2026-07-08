namespace RetailPOS.Api.Orders;

public static class OrderUploadEndpoints
{
    public static RouteGroupBuilder MapOrderUploadEndpoints(this RouteGroupBuilder api)
    {
        api.MapPost("/orders", async (
                OrderUploadRequest request,
                IOrderUploadHandler handler,
                CancellationToken cancellationToken) =>
            {
                var validation = request.Validate();
                if (!validation.Succeeded)
                {
                    return Results.ValidationProblem(validation.Errors);
                }

                var response = await handler.UploadAsync(request, cancellationToken);
                return Results.Ok(response);
            })
            .WithName("UploadOrder")
            .Produces<OrderUploadResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem();

        return api;
    }
}
