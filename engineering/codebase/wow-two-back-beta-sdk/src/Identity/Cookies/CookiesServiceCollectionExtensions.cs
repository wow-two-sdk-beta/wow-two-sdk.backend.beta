using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Cookies;

/// <summary>Cookie auth registration helpers.</summary>
public static class CookiesServiceCollectionExtensions
{
    /// <summary>Register cookie authentication with secure defaults (HTTP-only, SameSite=Lax, HTTPS-only).</summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configure">Optional override of cookie name, expiration, paths, and challenge mode.</param>
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

                if (opts.Mode == AuthChallengeMode.Api)
                {
                    // fetch auto-follows 3xx and never sees a 302 — emit raw status instead.
                    b.Events.OnRedirectToLogin = ctx =>
                    {
                        ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        return Task.CompletedTask;
                    };
                    b.Events.OnRedirectToAccessDenied = ctx =>
                    {
                        ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                        return Task.CompletedTask;
                    };
                }
            });

        return services;
    }
}
