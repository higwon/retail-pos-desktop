using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RetailPOS.Infrastructure.Configuration;
using RetailPOS.Infrastructure.Persistence;

namespace RetailPOS.Infrastructure.DependencyInjection;

public static class PersistenceServiceRegistration
{
    public static IServiceCollection AddLocalPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<LocalDatabaseOptions>()
            .Bind(configuration.GetSection(LocalDatabaseOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.DatabasePath),
                "Local database path is required.")
            .ValidateOnStart();

        services.AddDbContext<LocalPosDbContext>((provider, options) =>
        {
            var databasePath = provider.GetRequiredService<IOptions<LocalDatabaseOptions>>().Value.DatabasePath;
            var connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = Path.GetFullPath(databasePath),
                ForeignKeys = true
            }.ToString();

            options.UseSqlite(connectionString);
        });
        services.AddTransient<LocalDatabaseInitializer>();

        return services;
    }
}
