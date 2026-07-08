namespace RetailPOS.Api.Orders;

using Microsoft.AspNetCore.Mvc;

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

                try
                {
                    var response = await handler.UploadAsync(request, cancellationToken);
                    return Results.Ok(response);
                }
                catch (OrderUploadConflictException exception)
                {
                    return Results.Conflict(new ProblemDetails
                    {
                        Title = "Order upload conflict",
                        Detail = exception.Message,
                        Status = StatusCodes.Status409Conflict
                    });
                }
            })
            .WithName("UploadOrder")
            .Produces<OrderUploadResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);

        return api;
    }
}
