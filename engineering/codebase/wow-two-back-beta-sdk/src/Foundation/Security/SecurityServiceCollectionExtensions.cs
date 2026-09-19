using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace WoW.Two.Sdk.Backend.Beta.Foundation.Security;

/// <summary>Registration helpers for envelope cryptography.</summary>
public static class SecurityServiceCollectionExtensions
{
    /// <summary>Registers envelope cryptography — <see cref="IValueCipher"/> (value encryption), <see cref="ISealService"/> (KEK key management), and the default env-backed <see cref="IMasterKeyBroker"/>.</summary>
    /// <remarks>All singletons: the seal keeper must outlive a request to hold the unsealed key. Registration is <c>TryAdd</c>-based, so register a KMS/HSM-backed <see cref="IMasterKeyBroker"/> before this call to swap the key source. Call <see cref="ISealService.Unseal"/> at startup to load the key.</remarks>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configure">Optional override of the master-key env-var name.</param>
    public static IServiceCollection AddEnvelopeCryptography(
        this IServiceCollection services,
        Action<EnvelopeCryptographyOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = new EnvelopeCryptographyOptions();
        configure?.Invoke(options);
        services.TryAddSingleton(options);

        services.TryAddSingleton<IMasterKeyBroker, EnvironmentMasterKeyBroker>();
        services.TryAddSingleton<ISealService, SealService>();
        services.TryAddSingleton<IValueCipher, ValueCipher>();

        return services;
    }
}
