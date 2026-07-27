using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace WoW.Two.Sdk.Backend.Beta.Foundation.Security;

/// <summary>Registration helpers for envelope cryptography.</summary>
public static class SecurityServiceCollectionExtensions
{
    /// <summary>Registers envelope cryptography — <see cref="ICryptoCore"/> (value encryption), <see cref="ISealKeeper"/> (KEK key management), and the default env-backed <see cref="IMasterKeyProvider"/>.</summary>
    /// <remarks>All singletons: the seal keeper must outlive a request to hold the unsealed key. Registration is <c>TryAdd</c>-based, so register a KMS/HSM-backed <see cref="IMasterKeyProvider"/> before this call to swap the key source. Call <see cref="ISealKeeper.Unseal"/> at startup to load the key.</remarks>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configure">Optional override of the master-key env-var name.</param>
    public static IServiceCollection AddEnvelopeCryptography(
        this IServiceCollection services,
        Action<EnvelopeCryptographyOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (configure is not null)
            services.Configure(configure);
        else
            services.AddOptions<EnvelopeCryptographyOptions>();

        services.TryAddSingleton<IMasterKeyProvider, EnvironmentMasterKeyProvider>();
        services.TryAddSingleton<ISealKeeper, MasterKeySealKeeper>();
        services.TryAddSingleton<ICryptoCore, CryptoCore>();

        return services;
    }
}
