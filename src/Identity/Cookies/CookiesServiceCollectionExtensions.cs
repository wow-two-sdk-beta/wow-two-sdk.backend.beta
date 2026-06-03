using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Cookies;

/// <summary>Cookie authentication options.</summary>
public sealed record CookieAuthOptions
{
    /// <summary>Cookie name.</summary>
    public string CookieName { get; init; } = ".app.auth";

    /// <summary>Cookie expiration.</summary>
    public TimeSpan ExpireTimeSpan { get; init; } = TimeSpan.FromHours(8);

    /// <summary>Use sliding expiration. Default <c>true</c>.</summary>
    public bool SlidingExpiration { get; init; } = true;

    /// <summary>Login path. Default <c>/auth/login</c>.</summary>
    public PathString LoginPath { get; init; } = "/auth/login";

    /// <summary>Logout path. Default <c>/auth/logout</c>.</summary>
    public PathString LogoutPath { get; init; } = "/auth/logout";
}

/// <summary>Cookie auth registration helpers.</summary>
public static class CookiesServiceCollectionExtensions
{
    /// <summary>Register cookie authentication with secure defaults (HTTP-only, SameSite=Lax, HTTPS-only).</summary>
    public static IServiceCollection AddCookieAuthentication(this IServiceCollection services, Action<CookieAuthOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var opts = new CookieAuthOptions();
        configure?.Invoke(opts);

        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(b =>
            {
                b.Cookie.Name = opts.CookieName;
                b.Cookie.HttpOnly = true;
                b.Cookie.SameSite = SameSiteMode.Lax;
                b.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                b.ExpireTimeSpan = opts.ExpireTimeSpan;
                b.SlidingExpiration = opts.SlidingExpiration;
                b.LoginPath = opts.LoginPath;
                b.LogoutPath = opts.LogoutPath;
            });

        return services;
    }
}
