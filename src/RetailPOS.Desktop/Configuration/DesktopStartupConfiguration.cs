using Microsoft.Extensions.Configuration;

namespace RetailPOS.Desktop.Configuration;

public sealed record DesktopStartupConfiguration(
    ValidatedDesktopConfiguration Values,
    Serilog.ILogger Logger)
{
    public static DesktopStartupConfiguration Create(
        IConfiguration configuration,
        Func<IConfiguration, Serilog.ILogger> configuredLoggerFactory)
    {
        var values = DesktopConfigurationValidator.Validate(configuration);
        var logger = configuredLoggerFactory(configuration);
        return new(values, logger);
    }
}
