using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Guest;

/// <summary>Guest-session registration.</summary>
public static class GuestSessionServiceCollectionExtensions
{
    /// <summary>Registers <see cref="IGuestSession"/> backed by <see cref="CookieGuestSession"/>, issuing an idempotent anonymous-id cookie; pair with <c>AddCurrentUser</c> on a matching cookie name to read the guest back.</summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configure">Optional override of the cookie name, lifetime, and SameSite policy.</param>
    public static IServiceCollection AddGuestSession(
        this IServiceCollection services,
        Action<GuestSessionOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (configure is not null)
        {
            services.Configure(configure);
        }
        else
        {
            services.AddOptions<GuestSessionOptions>();
        }

        services.AddHttpContextAccessor();
        services.TryAddScoped<IGuestSession, CookieGuestSession>();
        return services;
    }
}
