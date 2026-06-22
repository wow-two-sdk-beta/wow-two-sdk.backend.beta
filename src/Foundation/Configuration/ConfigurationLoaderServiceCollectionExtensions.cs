using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace WoW.Two.Sdk.Backend.Beta.Foundation.Configuration;

/// <summary>Registration helpers that bind settings via <see cref="ConfigurationLoader"/> (appsettings section and environment-variable overlay).</summary>
public static class ConfigurationLoaderServiceCollectionExtensions
{
    /// <summary>Binds <typeparamref name="T"/> through <see cref="ConfigurationLoader.Load{T}"/> and registers the resolved instance as <see cref="IOptions{T}"/>.</summary>
    /// <typeparam name="T">The settings type to bind, overlay, and register.</typeparam>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configuration">The configuration the section is bound from.</param>
    /// <param name="sectionName">The section name to bind from. Defaults to the name of <typeparamref name="T"/> when null.</param>
    /// <returns>The same service collection so calls can be chained.</returns>
    public static IServiceCollection AddEnvironmentOverlaidOptions<T>(
        this IServiceCollection services,
        IConfiguration configuration,
        string? sectionName = null)
        where T : class, new()
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var settings = ConfigurationLoader.Load<T>(configuration, sectionName);

        services.AddSingleton(settings);
        services.AddSingleton<IOptions<T>>(new OptionsWrapper<T>(settings));

        return services;
    }
}
