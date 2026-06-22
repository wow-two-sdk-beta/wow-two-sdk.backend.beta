using Microsoft.Extensions.DependencyInjection;
using Refit;
using WoW.Two.Sdk.Backend.Beta.Foundation.Serialization;
using WoW.Two.Sdk.Backend.Beta.Http.Resilience;

namespace WoW.Two.Sdk.Backend.Beta.Http.Refit;

/// <summary>Provides registration for declarative Refit API clients with SDK JSON, a base address, and the standard resilience pipeline.</summary>
public static class RefitClientServiceCollectionExtensions
{
    /// <summary>Builds SDK-conventional Refit settings (System.Text.Json using <see cref="JsonOptionsPresets.Default"/>); returns a fresh instance per call.</summary>
    public static RefitSettings CreateDefaultRefitSettings() => new()
    {
        ContentSerializer = new SystemTextJsonContentSerializer(JsonOptionsPresets.Default),
    };

    /// <summary>Registers Refit typed client <typeparamref name="TApi"/> at <paramref name="baseAddress"/> using SDK JSON and the SDK resilience pipeline (retry, circuit breaker, and timeouts).</summary>
    /// <typeparam name="TApi">The Refit API interface.</typeparam>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="baseAddress">The API base address.</param>
    /// <param name="configureResilience">Optional override of the SDK resilience defaults.</param>
    /// <param name="configureClient">Optional further configuration of the underlying client.</param>
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

    /// <summary>Overload taking a base address as a string; throws if it is not a valid absolute URI.</summary>
    /// <typeparam name="TApi">The Refit API interface.</typeparam>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="baseAddress">The API base address.</param>
    /// <param name="configureResilience">Optional override of the SDK resilience defaults.</param>
    /// <param name="configureClient">Optional further configuration of the underlying client.</param>
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
