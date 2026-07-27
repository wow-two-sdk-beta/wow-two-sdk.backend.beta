using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Backend.Beta.Http.Resilience;

namespace WoW.Two.Sdk.Backend.Beta.Http.Core;

/// <summary>Provides registration for plain <see cref="HttpClient"/> typed clients (no Refit) with the SDK resilience pipeline.</summary>
public static class TypedClientServiceCollectionExtensions
{
    /// <summary>Registers typed client <typeparamref name="TClient"/> with <paramref name="baseAddress"/> and the SDK resilience pipeline (retry, circuit breaker, and timeouts).</summary>
    /// <typeparam name="TClient">The typed-client class.</typeparam>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="baseAddress">The API base address.</param>
    /// <param name="configureResilience">Optional override of the SDK resilience defaults.</param>
    /// <param name="configureClient">Optional further configuration of the underlying client.</param>
    public static IHttpClientBuilder AddResilientClient<TClient>(
        this IServiceCollection services,
        Uri baseAddress,
        Action<HttpResilienceOptions>? configureResilience = null,
        Action<HttpClient>? configureClient = null)
        where TClient : class
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(baseAddress);

        return services
            .AddHttpClient<TClient>(client =>
            {
                client.BaseAddress = baseAddress;
                configureClient?.Invoke(client);
            })
            .AddSdkResilience(configureResilience);
    }

    /// <summary>Registers a named <see cref="HttpClient"/> with <paramref name="baseAddress"/> and the SDK resilience pipeline. Resolve via <see cref="IHttpClientFactory.CreateClient(string)"/>.</summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="name">The logical name of the client.</param>
    /// <param name="baseAddress">The API base address.</param>
    /// <param name="configureResilience">Optional override of the SDK resilience defaults.</param>
    /// <param name="configureClient">Optional further configuration of the underlying client.</param>
    public static IHttpClientBuilder AddResilientClient(
        this IServiceCollection services,
        string name,
        Uri baseAddress,
        Action<HttpResilienceOptions>? configureResilience = null,
        Action<HttpClient>? configureClient = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(baseAddress);

        return services
            .AddHttpClient(name, client =>
            {
                client.BaseAddress = baseAddress;
                configureClient?.Invoke(client);
            })
            .AddSdkResilience(configureResilience);
    }
}
