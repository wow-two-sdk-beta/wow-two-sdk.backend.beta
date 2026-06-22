using System.Reflection;
using Microsoft.Extensions.Configuration;

namespace WoW.Two.Sdk.Backend.Beta.Foundation.Configuration;

/// <summary>Binds a configuration section (named after the type) then overlays <see cref="EnvironmentVariableAttribute"/>-marked properties from environment variables, treating empty values as absent.</summary>
/// <remarks>Section name defaults to the type name; env overlay wins over appsettings; an empty env var is treated as null and does not override.</remarks>
public static class ConfigurationLoader
{
    /// <summary>Builds a settings object of type <typeparamref name="T"/> from a configuration section and an environment-variable overlay.</summary>
    /// <typeparam name="T">The settings type to bind and overlay.</typeparam>
    /// <param name="configuration">The configuration the section is bound from.</param>
    /// <param name="sectionName">The section name to bind from. Defaults to the name of <typeparamref name="T"/> when null.</param>
    /// <returns>The bound and env-overlaid instance of <typeparamref name="T"/>.</returns>
    /// <exception cref="InvalidOperationException">Thrown when a property marked required has no value after binding and overlay.</exception>
    public static T Load<T>(IConfiguration configuration, string? sectionName = null)
        where T : class, new()
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var section = sectionName ?? typeof(T).Name;
        var instance = configuration.GetSection(section).Get<T>() ?? new T();

        var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var property in properties)
        {
            var attribute = property.GetCustomAttribute<EnvironmentVariableAttribute>();
            if (attribute is null)
                continue;

            // Overlay the env-var value when present and writable; empty is treated as absent.
            var envValue = NullIfEmpty(Environment.GetEnvironmentVariable(attribute.Name));
            if (envValue is not null && property.CanWrite)
            {
                property.SetValue(instance, envValue);
                continue;
            }

            // A required property with no bound and no env value is a hard configuration error.
            if (attribute.Required && string.IsNullOrWhiteSpace(property.GetValue(instance) as string))
                throw new InvalidOperationException(
                    $"Configuration '{property.Name}' not found. " +
                    $"Set env var '{attribute.Name}' or appsettings section '{section}:{property.Name}'.");
        }

        return instance;
    }

    /// <summary>Returns null when the value is null, empty, or whitespace-only; otherwise the value unchanged.</summary>
    /// <param name="value">The value to normalize.</param>
    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}
