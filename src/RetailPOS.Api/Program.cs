using RetailPOS.Api;
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog((context, _, configuration) =>
{
    configuration
        .MinimumLevel.Is(ParseMinimumLevel(context.Configuration["Serilog:MinimumLevel:Default"]))
        .MinimumLevel.Override(
            "Microsoft.AspNetCore",
            ParseMinimumLevel(context.Configuration["Serilog:MinimumLevel:Override:Microsoft.AspNetCore"], LogEventLevel.Warning))
        .Enrich.FromLogContext()
        .WriteTo.Console(
            outputTemplate:
            "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext} {Message:lj} {Properties:j}{NewLine}{Exception}");
});
builder.Services.AddRetailPosApi();

var app = builder.Build();
app.UseRetailPosApi();

app.Run();

static LogEventLevel ParseMinimumLevel(string? value, LogEventLevel fallback = LogEventLevel.Information) =>
    Enum.TryParse<LogEventLevel>(value, ignoreCase: true, out var level)
        ? level
        : fallback;

public partial class Program;
