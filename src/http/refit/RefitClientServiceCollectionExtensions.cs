using Microsoft.Extensions.DependencyInjection;
using Refit;
using WoW.Two.Sdk.Backend.Beta.Foundation.Serialization;
using WoW.Two.Sdk.Backend.Beta.http.resilience;

namespace WoW.Two.Sdk.Backend.Beta.http.refit;

/// <summary>
/// Registration helpers for declarative Refit API clients with SDK conventions baked in:
/// SDK JSON serialization, a base address, and the standard resilience pipeline.
/// </summary>
public static class RefitClientServiceCollectionExtensions
{
    /// <summary>
    /// Builds SDK-conventional Refit settings — System.Text.Json using <see cref="JsonOptionsPresets.Default"/>.
    /// A factory (not a cached static) so any failure surfaces at the call site rather than poisoning the type's
    /// static initializer, and so callers always get a fresh, independently-configurable instance.
    /// </summary>
    public static RefitSettings CreateDefaultRefitSettings() => new()
    {
        ContentSerializer = new SystemTextJsonContentSerializer(JsonOptionsPresets.Default),
    };

    /// <summary>
    /// Registers a Refit typed client <typeparamref name="TApi"/> pointed at <paramref name="baseAddress"/>,
    /// using SDK JSON settings and the SDK resilience pipeline (retry + circuit breaker + timeouts).
    /// </summary>
    /// <typeparam name="TApi">The Refit API interface.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="baseAddress">The API base address.</param>
    /// <param name="configureResilience">Optional override of the resilience defaults.</param>
    /// <param name="configureClient">Optional additional <see cref="HttpClient"/> configuration (headers, timeout, …).</param>
    public static IHttpClientBuilder AddRefitApiClient<TApi>(
        this IServiceCollection services,
        Uri baseAddress,
        Action<HttpResilienceOptions>? configureResilience = null,
        Action<HttpClient>? configureClient = null)
        where TApi : class
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(baseAddress);

        return services
            .AddRefitClient<TApi>(CreateDefaultRefitSettings())
            .ConfigureHttpClient(client =>
            {
                client.BaseAddress = baseAddress;
                configureClient?.Invoke(client);
            })
            .AddSdkResilience(configureResilience);
    }

    /// <summary>
    /// Overload taking a base address as a string. Throws if it is not a valid absolute URI.
    /// </summary>
    public static IHttpClientBuilder AddRefitApiClient<TApi>(
        this IServiceCollection services,
        string baseAddress,
        Action<HttpResilienceOptions>? configureResilience = null,
        Action<HttpClient>? configureClient = null)
        where TApi : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseAddress);
        return services.AddRefitApiClient<TApi>(new Uri(baseAddress, UriKind.Absolute), configureResilience, configureClient);
    }
}
