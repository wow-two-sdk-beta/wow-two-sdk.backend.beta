using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Cookies;

/// <summary>How the cookie handler challenges an unauthenticated/unauthorized request.</summary>
public enum AuthChallengeMode
{
    /// <summary>Server-rendered MVC: 302 redirect to <see cref="CookieAuthOptions.LoginPath"/> / access-denied page.</summary>
    Mvc,

    /// <summary>SPA and API: raw 401/403, no redirect — client renders its own sign-in.</summary>
    Api,
}
