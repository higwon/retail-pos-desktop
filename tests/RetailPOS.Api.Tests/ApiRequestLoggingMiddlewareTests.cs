using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using RetailPOS.Api;

namespace RetailPOS.Api.Tests;

public sealed class ApiRequestLoggingMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_ContinuesPipelineAndPreservesStatusCode()
    {
        var context = new DefaultHttpContext();
        var middleware = new ApiRequestLoggingMiddleware(
            nextContext =>
            {
                nextContext.Response.StatusCode = StatusCodes.Status204NoContent;
                return Task.CompletedTask;
            },
            NullLogger<ApiRequestLoggingMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status204NoContent, context.Response.StatusCode);
    }
}
