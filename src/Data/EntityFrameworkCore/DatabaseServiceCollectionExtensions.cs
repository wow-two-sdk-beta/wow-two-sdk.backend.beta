using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace WoW.Two.Sdk.Backend.Beta.Data.EntityFrameworkCore;

/// <summary>Registration helpers for <see cref="DatabaseOptions"/>.</summary>
public static class DatabaseServiceCollectionExtensions
{
    /// <summary>Binds <see cref="DatabaseOptions"/> from the given configuration section (default <c>Database</c>).</summary>
    public static IServiceCollection AddDatabaseOptions(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName = "Database")
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<DatabaseOptions>()
            .Bind(configuration.GetSection(sectionName))
            .ValidateOnStart();

        return services;
    }

    /// <summary>Registers <see cref="DatabaseOptions"/> from an inline configurator.</summary>
    public static IServiceCollection AddDatabaseOptions(
        this IServiceCollection services,
        Action<DatabaseOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.AddOptions<DatabaseOptions>()
            .Configure(configure)
            .ValidateOnStart();

        return services;
    }
}
