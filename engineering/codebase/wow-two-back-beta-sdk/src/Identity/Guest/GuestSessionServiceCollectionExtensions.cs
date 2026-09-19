using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WoW.Two.Sdk.Backend.Beta.Identity.Guest.Services;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Guest;

/// <summary>Guest-session registration.</summary>
public static class GuestSessionServiceCollectionExtensions
{
    /// <summary>Registers <see cref="IGuestSessionService"/> backed by <see cref="CookieGuestSessionService"/>, issuing an idempotent anonymous-id cookie; pair with <c>AddCurrentUser</c> on a matching cookie name to read the guest back.</summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configure">Optional override of the cookie name, lifetime, and SameSite policy.</param>
    public static IServiceCollection AddGuestSession(
        this IServiceCollection services,
        Action<GuestSessionOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = new GuestSessionOptions();
        configure?.Invoke(options);
        services.TryAddSingleton(options);

        services.AddHttpContextAccessor();
        services.TryAddScoped<IGuestSessionService, CookieGuestSessionService>();
        return services;
    }
}
