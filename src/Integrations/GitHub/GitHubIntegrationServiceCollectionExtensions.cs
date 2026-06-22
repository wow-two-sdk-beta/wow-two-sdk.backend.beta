using System.Net.Http.Headers;
using Microsoft.Extensions.DependencyInjection;

namespace WoW.Two.Sdk.Backend.Beta.Integrations.GitHub;

/// <summary>GitHub integration registration.</summary>
public static class GitHubIntegrationServiceCollectionExtensions
{
    private const string ApiBaseAddress = "https://api.github.com/";
    private const string UserAgent = "wow-two-sdk";
    private const string AcceptMediaType = "application/vnd.github+json";

    /// <summary>Registers a typed <see cref="HttpClient"/> for the GitHub REST API (base <c>https://api.github.com/</c>) bound to <see cref="IGitHubClient"/>. Each call carries a Bearer token from <see cref="IAccessTokenProvider"/> — register one (e.g. <c>AddHttpContextAccessTokenProvider</c>).</summary>
    /// <param name="services">The service collection to configure.</param>
    public static IHttpClientBuilder AddGitHubIntegration(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddHttpClient<IGitHubClient, GitHubClient>(client =>
        {
            client.BaseAddress = new Uri(ApiBaseAddress);
            client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(AcceptMediaType));
        });
    }
}
