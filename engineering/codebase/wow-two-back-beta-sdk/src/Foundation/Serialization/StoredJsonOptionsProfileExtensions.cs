using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace WoW.Two.Sdk.Backend.Beta.Foundation.Serialization;

/// <summary>Provides keyed registration and resolution for pinned stored-JSON options profiles.</summary>
public static class StoredJsonOptionsProfileExtensions
{
    /// <summary>Registers an immutable stored-JSON options profile under <paramref name="key"/>.</summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="key">The application-owned stored-format key.</param>
    /// <param name="options">The pinned options used for every operation on that stored format.</param>
    public static IServiceCollection AddStoredJsonOptionsProfile(
        this IServiceCollection services,
        string key,
        JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(options);

        if (!options.IsReadOnly)
        {
            throw new ArgumentException(
                "A stored JSON options profile must be read-only before registration.",
                nameof(options));
        }

        services.TryAddKeyedSingleton(key, options);
        return services;
    }

    /// <summary>Creates and registers a stored-JSON options profile under <paramref name="key"/>.</summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="key">The application-owned stored-format key.</param>
    /// <param name="modifiers">Type-info modifiers for unions declared outside their types.</param>
    public static IServiceCollection AddStoredJsonOptionsProfile(
        this IServiceCollection services,
        string key,
        params Action<JsonTypeInfo>[] modifiers)
    {
        ArgumentNullException.ThrowIfNull(modifiers);
        return services.AddStoredJsonOptionsProfile(key, StoredJsonOptionsFactory.Create(modifiers));
    }

    /// <summary>Gets the stored-JSON options profile registered under <paramref name="key"/>.</summary>
    /// <param name="services">The service provider containing the profile.</param>
    /// <param name="key">The application-owned stored-format key.</param>
    /// <exception cref="InvalidOperationException">No stored-JSON options profile has the requested key.</exception>
    public static JsonSerializerOptions GetRequiredStoredJsonOptionsProfile(
        this IServiceProvider services,
        string key)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        return services.GetKeyedService<JsonSerializerOptions>(key)
            ?? throw new InvalidOperationException(
                $"No stored JSON options profile is registered for key '{key}'.");
    }
}
