using Microsoft.Extensions.DependencyInjection;

namespace WoW.Two.Sdk.Backend.Beta.Data.Migrations.DbUp;

/// <summary>Registration helpers for the DbUp runner.</summary>
public static class DbUpServiceCollectionExtensions
{
    /// <summary>Registers the DbUp hosted service with the given options.</summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configure">A hook to configure the DbUp runner options.</param>
    public static IServiceCollection AddDbUpRunner(
        this IServiceCollection services,
        Action<DbUpOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.AddOptions<DbUpOptions>()
            .Configure(configure)
            .ValidateOnStart();
        services.AddHostedService<DbUpHostedService>();
        return services;
    }
}
