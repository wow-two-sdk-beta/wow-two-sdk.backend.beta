using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WoW.Two.Sdk.Backend.Beta.Foundation.Options;
using WoW.Two.Sdk.Backend.Beta.Geo.IpLocation.BackgroundServices;
using WoW.Two.Sdk.Backend.Beta.Geo.IpLocation.Brokers;
using WoW.Two.Sdk.Backend.Beta.Geo.IpLocation.Services;

namespace WoW.Two.Sdk.Backend.Beta.Geo.IpLocation;

/// <summary>Provides registration for IP location over MaxMind DB files.</summary>
public static class IpLocationServiceCollectionExtensions
{
    /// <summary>Registers <see cref="IIpLocationBroker"/> over a provisioned MMDB file.</summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configure">Sets the database path and reload interval.</param>
    public static IServiceCollection AddMmdbIpLocation(
        this IServiceCollection services,
        Action<MmdbIpLocationOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddValidatedOptions(
            configure,
            static builder => builder
                .Validate(static options => !string.IsNullOrWhiteSpace(options.DatabasePath), "IpLocation: DatabasePath is required.")
                .Validate(static options => options.ReloadCheckInterval > TimeSpan.Zero, "IpLocation: ReloadCheckInterval must be positive."));
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IIpLocationBroker, MmdbIpLocationBroker>();
        return services;
    }

    /// <summary>Registers <see cref="IIpLocationBroker"/> over the free DB-IP Lite database, downloaded monthly.</summary>
    /// <remarks>DB-IP Lite is licensed CC BY 4.0: show "IP Geolocation by DB-IP" linking to https://db-ip.com.</remarks>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configure">Sets the writable directory, edition and refresh interval.</param>
    public static IServiceCollection AddDbIpLiteIpLocation(
        this IServiceCollection services,
        Action<DbIpLiteOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.AddValidatedOptions(
            configure,
            static builder => builder
                .Validate(static options => !string.IsNullOrWhiteSpace(options.DatabaseDirectory), "DbIpLite: DatabaseDirectory is required.")
                .Validate(static options => options.RefreshInterval > TimeSpan.Zero, "DbIpLite: RefreshInterval must be positive.")
                .Validate(static options => options.MaxDatabaseBytes > 0, "DbIpLite: MaxDatabaseBytes must be positive.")
                .Validate(static options => Enum.IsDefined(options.Edition), "DbIpLite: Edition must be defined."));
        services.AddOptions<MmdbIpLocationOptions>()
            .Configure<DbIpLiteOptions>(static (mmdb, dbIp) => mmdb.DatabasePath = DbIpLiteDownloadService.PathFor(dbIp));
        services.AddMmdbIpLocation();
        services.AddHttpClient(IpLocationConstants.DbIpLiteHttpClientName);
        services.TryAddSingleton<DbIpLiteDownloadService>();
        services.AddHostedService<DbIpLiteDownloadBackgroundService>();
        return services;
    }
}
