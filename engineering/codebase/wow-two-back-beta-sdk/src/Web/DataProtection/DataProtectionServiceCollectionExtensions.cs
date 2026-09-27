using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WoW.Two.Sdk.Backend.Beta.Foundation.Options;

namespace WoW.Two.Sdk.Backend.Beta.Web.DataProtection;

/// <summary>Provides registration for a persistent Data Protection key ring.</summary>
public static class DataProtectionServiceCollectionExtensions
{
    /// <summary>Registers Data Protection with a stable application name and a key ring stored on disk.</summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configure">Sets the application name and key directory.</param>
    /// <remarks>
    ///   - outside Development, startup fails without a key directory: ephemeral keys void every cookie on restart
    ///   - authentication cookies, antiforgery tokens and guest capabilities stay valid across container replacement
    ///   - the key ring is stored unencrypted; restrict access to the volume
    /// </remarks>
    public static IServiceCollection AddPersistentDataProtection(
        this IServiceCollection services,
        Action<PersistentDataProtectionOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.AddValidatedOptions(
            configure,
            static builder => builder
                .Validate(static options => !string.IsNullOrWhiteSpace(options.ApplicationName), "DataProtection: ApplicationName is required.")
                .Validate<IHostEnvironment>(
                    static (options, environment) => environment.IsDevelopment() || !string.IsNullOrWhiteSpace(options.KeyDirectory),
                    "DataProtection: KeyDirectory is required outside Development."));
        services.AddDataProtection();
        services.AddOptions<DataProtectionOptions>()
            .Configure<PersistentDataProtectionOptions>(static (protection, options) =>
                protection.ApplicationDiscriminator = options.ApplicationName);
        services.AddOptions<KeyManagementOptions>()
            .Configure<PersistentDataProtectionOptions, ILoggerFactory>(static (keys, options, loggers) =>
            {
                if (!string.IsNullOrWhiteSpace(options.KeyDirectory))
                    keys.XmlRepository = new FileSystemXmlRepository(new DirectoryInfo(options.KeyDirectory), loggers);
            });
        return services;
    }
}
