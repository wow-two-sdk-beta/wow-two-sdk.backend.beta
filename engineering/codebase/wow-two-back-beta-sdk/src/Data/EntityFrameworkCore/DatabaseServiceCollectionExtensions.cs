using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace WoW.Two.Sdk.Backend.Beta.Data.EntityFrameworkCore;

/// <summary>Registration helpers for <see cref="DatabaseSettings"/>.</summary>
public static class DatabaseServiceCollectionExtensions
{
    /// <summary>Binds <see cref="DatabaseSettings"/> from the given configuration section (default <c>DatabaseSettings</c>).</summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configuration">The configuration the section is bound from.</param>
    /// <param name="sectionName">The configuration section name. Default <c>DatabaseSettings</c>.</param>
    public static IServiceCollection AddDatabaseSettings(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName = nameof(DatabaseSettings))
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<DatabaseSettings>()
            .Bind(configuration.GetSection(sectionName))
            .ValidateOnStart();

        // Consumers take the record, never the wrapper — the bind pipeline stays for validation and reload.
        services.AddSingleton(serviceProvider => serviceProvider.GetRequiredService<IOptions<DatabaseSettings>>().Value);

        return services;
    }

    /// <summary>Registers <see cref="DatabaseSettings"/> from a connection string supplied in code.</summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="connectionString">The database connection string.</param>
    public static IServiceCollection AddDatabaseSettings(this IServiceCollection services, string connectionString)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        var settings = new DatabaseSettings { ConnectionString = connectionString };
        services.AddSingleton(settings);
        services.AddSingleton<IOptions<DatabaseSettings>>(Options.Create(settings));

        return services;
    }
}
