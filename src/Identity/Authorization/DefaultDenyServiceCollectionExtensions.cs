using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Authorization;

/// <summary>Default-deny authorization registration.</summary>
public static class DefaultDenyServiceCollectionExtensions
{
    /// <summary>Name of the registered default-deny policy, also wired as the fallback policy so endpoints without <c>[Authorize]</c>/<c>[AllowAnonymous]</c> are denied; anonymous endpoints (e.g. <c>/health</c>) MUST opt out with <c>[AllowAnonymous]</c>.</summary>
    public const string PolicyName = "DefaultDeny";

    /// <summary>Builds a default-deny policy requiring an authenticated principal on <paramref name="scheme"/> (optionally allowlisted), registers it under <see cref="PolicyName"/>, and sets it as the fallback policy; a single named scheme (e.g. the cookie) yields a clean 401 instead of an external-IdP 302.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="scheme">Authentication scheme the policy requires (e.g. the cookie scheme).</param>
    /// <param name="withAllowlist">When <c>true</c>, also enforce <see cref="AllowlistRequirement"/>.</param>
    public static IServiceCollection AddDefaultDenyAuthorization(
        this IServiceCollection services,
        string scheme,
        bool withAllowlist = false)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(scheme);

        var builder = new AuthorizationPolicyBuilder(scheme)
            .RequireAuthenticatedUser();

        if (withAllowlist)
        {
            builder.AddRequirements(new AllowlistRequirement());
        }

        var policy = builder.Build();

        services.AddAuthorizationBuilder()
            .AddPolicy(PolicyName, policy)
            .SetFallbackPolicy(policy);

        return services;
    }
}
