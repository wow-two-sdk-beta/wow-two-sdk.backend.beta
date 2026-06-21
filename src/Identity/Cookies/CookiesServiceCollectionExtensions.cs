using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Cookies;

/// <summary>How the cookie handler challenges an unauthenticated/unauthorized request.</summary>
public enum AuthChallengeMode
{
    /// <summary>Server-rendered MVC: 302 redirect to <see cref="CookieAuthOptions.LoginPath"/> / access-denied page.</summary>
    Mvc,

    /// <summary>SPA + API: raw 401/403, no redirect — client renders its own sign-in.</summary>
    Api,
}

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

    /// <summary>Challenge style. Default <see cref="AuthChallengeMode.Mvc"/>.</summary>
    public AuthChallengeMode Mode { get; init; } = AuthChallengeMode.Mvc;
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
