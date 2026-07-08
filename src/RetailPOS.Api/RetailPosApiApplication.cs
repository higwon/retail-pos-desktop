using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Diagnostics;

namespace RetailPOS.Api;

public static class RetailPosApiApplication
{
    public static IServiceCollection AddRetailPosApi(this IServiceCollection services)
    {
        services.AddProblemDetails();
        services.AddRouting(options => options.LowercaseUrls = true);
        services.AddEndpointsApiExplorer();

        return services;
    }

    public static WebApplication UseRetailPosApi(this WebApplication app)
    {
        app.UseMiddleware<ApiRequestLoggingMiddleware>();
        app.UseMiddleware<ApiExceptionHandlingMiddleware>();

        app.UseStatusCodePages(async (StatusCodeContext statusCodeContext) =>
        {
            var httpContext = statusCodeContext.HttpContext;
            if (httpContext.Response.HasStarted || httpContext.Response.ContentLength is not null)
            {
                return;
            }

            httpContext.Response.ContentType = "application/problem+json";
            var problem = new ProblemDetails
            {
                Status = httpContext.Response.StatusCode,
                Title = "Request could not be completed",
                Instance = httpContext.Request.Path
            };

            await httpContext.Response.WriteAsJsonAsync(
                problem,
                (JsonSerializerOptions?)null,
                "application/problem+json",
                httpContext.RequestAborted);
        });

        var api = app.MapGroup("/api");
        api.MapGet("/health", () => Results.Ok(new HealthResponse("Healthy", DateTimeOffset.UtcNow)))
            .WithName("GetApiHealth")
            .Produces<HealthResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        return app;
    }
}
