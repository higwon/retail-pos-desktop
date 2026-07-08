using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
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

    [Fact]
    public async Task InvokeAsync_LogsFinalStatusCodeAfterExceptionHandling()
    {
        var context = new DefaultHttpContext
        {
            TraceIdentifier = "trace-500"
        };
        context.Request.Method = HttpMethods.Get;
        context.Request.Path = "/api/failure";
        context.Response.Body = new MemoryStream();

        var logger = new RecordingLogger<ApiRequestLoggingMiddleware>();
        var exceptionHandlingMiddleware = new ApiExceptionHandlingMiddleware(
            _ => throw new InvalidOperationException("boom"),
            NullLogger<ApiExceptionHandlingMiddleware>.Instance,
            new StubHostEnvironment("Production"));
        var requestLoggingMiddleware = new ApiRequestLoggingMiddleware(
            exceptionHandlingMiddleware.InvokeAsync,
            logger);

        await requestLoggingMiddleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Contains("responded 500", entry.Message, StringComparison.Ordinal);
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<LogEntry> Entries { get; } = [];

        public IDisposable BeginScope<TState>(TState state)
            where TState : notnull =>
            NullLogger.Instance.BeginScope(state);

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Entries.Add(new LogEntry(logLevel, formatter(state, exception)));
        }
    }

    private sealed record LogEntry(LogLevel Level, string Message);

    private sealed class StubHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "RetailPOS.Api.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
