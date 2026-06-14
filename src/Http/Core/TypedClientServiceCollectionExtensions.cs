using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Backend.Beta.Http.Resilience;

namespace WoW.Two.Sdk.Backend.Beta.Http.Core;

/// <summary>
/// Registration helpers for plain <see cref="HttpClient"/>-based typed clients (no Refit),
/// with the SDK resilience pipeline applied.
/// </summary>
public static class TypedClientServiceCollectionExtensions
{
    /// <summary>
    /// Registers a typed client <typeparamref name="TClient"/> with <paramref name="baseAddress"/> and the
    /// SDK resilience pipeline (retry + circuit breaker + timeouts). The client receives an injected
    /// <see cref="HttpClient"/> via its constructor.
    /// </summary>
    /// <typeparam name="TClient">The typed-client class.</typeparam>
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

    /// <summary>
    /// Registers a named <see cref="HttpClient"/> with <paramref name="baseAddress"/> and the SDK
    /// resilience pipeline. Resolve via <see cref="IHttpClientFactory.CreateClient(string)"/>.
    /// </summary>
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
