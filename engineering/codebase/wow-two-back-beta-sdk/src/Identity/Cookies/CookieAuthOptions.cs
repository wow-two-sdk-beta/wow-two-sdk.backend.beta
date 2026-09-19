using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Cookies;

/// <summary>Holds cookie authentication options.</summary>
public sealed record CookieAuthOptions
{
    /// <summary>Cookie name.</summary>
    public string CookieName { get; set; } = ".app.auth";

    /// <summary>Cookie expiration.</summary>
    public TimeSpan ExpireTimeSpan { get; set; } = TimeSpan.FromHours(8);

    /// <summary>Use sliding expiration. Default <c>true</c>.</summary>
    public bool SlidingExpiration { get; set; } = true;

    /// <summary>Login path. Default <c>/auth/login</c>.</summary>
    public PathString LoginPath { get; set; } = "/auth/login";

    /// <summary>Logout path. Default <c>/auth/logout</c>.</summary>
    public PathString LogoutPath { get; set; } = "/auth/logout";

    /// <summary>Challenge style. Default <see cref="AuthChallengeMode.Mvc"/>.</summary>
    public AuthChallengeMode Mode { get; set; } = AuthChallengeMode.Mvc;
}
