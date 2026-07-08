using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using RetailPOS.Api;

namespace RetailPOS.Api.Tests;

public sealed class ApiExceptionHandlingMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_ReturnsProblemDetailsWithoutTechnicalDetailsOutsideDevelopment()
    {
        var context = new DefaultHttpContext
        {
            TraceIdentifier = "trace-001"
        };
        context.Request.Path = "/api/orders";
        context.Response.Body = new MemoryStream();

        var middleware = new ApiExceptionHandlingMiddleware(
            _ => throw new InvalidOperationException("database connection string leaked"),
            NullLogger<ApiExceptionHandlingMiddleware>.Instance,
            new StubHostEnvironment("Production"));

        await middleware.InvokeAsync(context);

        context.Response.Body.Position = 0;
        using var document = await JsonDocument.ParseAsync(context.Response.Body);

        Assert.Equal((int)HttpStatusCode.InternalServerError, context.Response.StatusCode);
        Assert.Equal("application/problem+json", context.Response.ContentType);
        Assert.Equal("Unexpected server error", document.RootElement.GetProperty("title").GetString());
        Assert.Equal("An unexpected server error occurred.", document.RootElement.GetProperty("detail").GetString());
        Assert.Equal("trace-001", document.RootElement.GetProperty("traceId").GetString());
    }

    [Fact]
    public async Task InvokeAsync_ReturnsDevelopmentDetailWhenEnvironmentIsDevelopment()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/health";
        context.Response.Body = new MemoryStream();

        var middleware = new ApiExceptionHandlingMiddleware(
            _ => throw new InvalidOperationException("developer detail"),
            NullLogger<ApiExceptionHandlingMiddleware>.Instance,
            new StubHostEnvironment(Environments.Development));

        await middleware.InvokeAsync(context);

        context.Response.Body.Position = 0;
        using var document = await JsonDocument.ParseAsync(context.Response.Body);

        Assert.Equal("developer detail", document.RootElement.GetProperty("detail").GetString());
    }

    private sealed class StubHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "RetailPOS.Api.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
