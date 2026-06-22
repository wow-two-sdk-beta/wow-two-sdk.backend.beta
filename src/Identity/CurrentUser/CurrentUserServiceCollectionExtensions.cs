using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace WoW.Two.Sdk.Backend.Beta.Identity.CurrentUser;

/// <summary>Current-user resolver registration.</summary>
public static class CurrentUserServiceCollectionExtensions
{
    /// <summary>Registers <see cref="ICurrentUser"/> backed by <see cref="CookieCurrentUser"/>, resolving authenticated / guest / anonymous from the request; pair with <c>AddGuestSession</c> on a matching cookie name to issue guest ids.</summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configure">Optional override of the guest-cookie name and subject-claim type.</param>
    public static IServiceCollection AddCurrentUser(
        this IServiceCollection services,
        Action<CurrentUserOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (configure is not null)
        {
            services.Configure(configure);
        }
        else
        {
            services.AddOptions<CurrentUserOptions>();
        }

        services.AddHttpContextAccessor();
        services.TryAddScoped<ICurrentUser, CookieCurrentUser>();
        return services;
    }
}
