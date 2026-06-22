using Microsoft.Extensions.DependencyInjection;

namespace WoW.Two.Sdk.Backend.Beta.Integrations.Ghcr;

/// <summary>GHCR integration registration.</summary>
public static class GhcrIntegrationServiceCollectionExtensions
{
    private const string UserAgent = "wow-two-sdk";

    /// <summary>Registers a typed <see cref="HttpClient"/> for the GitHub Container Registry bound to <see cref="IContainerRegistryClient"/>. Private-image probes use a token from <see cref="IAccessTokenProvider"/> — register one (e.g. <c>AddHttpContextAccessTokenProvider</c>).</summary>
    /// <param name="services">The service collection to configure.</param>
    public static IHttpClientBuilder AddGhcrIntegration(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddHttpClient<IContainerRegistryClient, GhcrClient>(client =>
        {
            client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
        });
    }
}
