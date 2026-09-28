using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace WoW.Two.Sdk.Backend.Beta.Identity.ApiKeys;

/// <summary>API key authentication and access gate registration helpers.</summary>
public static class ApiKeyServiceCollectionExtensions
{
    /// <summary>Registers the <c>ApiKey</c> authentication scheme, the secret factory and reader, and the gate's options.</summary>
    /// <remarks>
    /// The product registers its <see cref="IApiKeyRepository"/> (scoped) and calls <see cref="UseApiKeyAccessGate"/> after
    /// authentication. The default scheme is left alone, so JWT or cookies keep theirs.
    /// </remarks>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configureKeys">Configures the secret shape — at least the product's marker.</param>
    /// <param name="configureGate">Configures the guarded, open and local-only paths and the local pass.</param>
    /// <returns>The same <paramref name="services"/> for chaining.</returns>
    public static IServiceCollection AddApiKeyAuthentication(
        this IServiceCollection services,
        Action<ApiKeyOptions> configureKeys,
        Action<ApiKeyAccessGateOptions>? configureGate = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureKeys);

        services.AddOptions<ApiKeyOptions>()
            .Configure(configureKeys)
            .Validate(options => !string.IsNullOrWhiteSpace(options.Marker), "ApiKeyOptions.Marker is required.")
            .Validate(options => options.RandomLength >= 24, "ApiKeyOptions.RandomLength must be at least 24.")
            .Validate(
                options => options.VisibleLength >= 0 && options.VisibleLength < options.RandomLength,
                "ApiKeyOptions.VisibleLength must show fewer characters than the secret has.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.HeaderName), "ApiKeyOptions.HeaderName is required.")
            .ValidateOnStart();
        services.AddOptions<ApiKeyAccessGateOptions>().Configure(configureGate ?? (_ => { }));
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<ApiKeySecretFactory>();
        services.TryAddSingleton<ApiKeySecretReader>();

        services.AddAuthentication()
            .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(ApiKeyAuthenticationDefaults.Scheme, null);
        return services;
    }

    /// <summary>Adds the API key gate; call it after <c>UseAuthentication</c> and before the guarded endpoints.</summary>
    /// <param name="app">The application builder.</param>
    /// <returns>The same <paramref name="app"/> for chaining.</returns>
    public static IApplicationBuilder UseApiKeyAccessGate(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.UseMiddleware<ApiKeyAccessGateMiddleware>();
    }
}
