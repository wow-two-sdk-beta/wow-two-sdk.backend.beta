using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace WoW.Two.Sdk.Backend.Beta.Identity.CurrentUser;

/// <summary>Current-user resolver registration.</summary>
public static class CurrentUserServiceCollectionExtensions
{
    /// <summary>Registers <see cref="ICurrentUserService"/> backed by <see cref="CookieCurrentUserService"/>, resolving authenticated / guest / anonymous from the ambient request; pair with <c>AddGuestSession</c> on a matching cookie name to issue guest ids.</summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configure">Optional override of the guest-cookie name and subject-claim type.</param>
    public static IServiceCollection AddCurrentUser(
        this IServiceCollection services,
        Action<CurrentUserOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = new CurrentUserOptions();
        configure?.Invoke(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.GuestCookieName);
        services.TryAddSingleton(options);

        services.AddDataProtection();
        services.TryAddSingleton(TimeProvider.System);
        services.AddHttpContextAccessor();
        services.TryAddSingleton<ICurrentUserService, CookieCurrentUserService>();
        return services;
    }
}
