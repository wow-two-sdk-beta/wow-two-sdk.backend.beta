using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Backend.Beta.Foundation.Errors;

namespace WoW.Two.Sdk.Backend.Beta.Mediator.Authorization;

/// <summary>Registration helper.</summary>
public static class AuthorizationBehaviorServiceCollectionExtensions
{
    /// <summary>Register the authorization pipeline behavior. Requires <c>AddHttpContextAccessor()</c>.</summary>
    /// <param name="services">The service collection to configure.</param>
    public static IServiceCollection AddMediatorAuthorizingInterceptor(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddHttpContextAccessor();
        services.AddAuthorization();
        return services.AddMediatorInterceptor(typeof(AuthorizingInterceptor<,>));
    }
}
