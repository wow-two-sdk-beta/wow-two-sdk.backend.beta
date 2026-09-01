using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace WoW.Two.Sdk.Backend.Beta.Integrations;

/// <summary>Access-token-provider registration helpers.</summary>
public static class AccessTokenProviderServiceCollectionExtensions
{
    /// <summary>Registers <see cref="HttpContextAccessTokenService"/> as the <see cref="IAccessTokenService"/>, adding <see cref="IHttpContextAccessor"/>.</summary>
    /// <param name="services">The service collection to configure.</param>
    public static IServiceCollection AddHttpContextAccessTokenProvider(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddHttpContextAccessor();
        services.TryAddScoped<IAccessTokenService, HttpContextAccessTokenService>();
        return services;
    }
}
