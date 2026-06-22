using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;

namespace WoW.Two.Sdk.Backend.Beta.Testing.Auth;

/// <summary>
/// Options for <see cref="TestAuthHandler"/> — the fixed identity every request authenticates as when the
/// test-auth scheme is the active handler. Tweak per test via <c>AddTestAuth</c>'s configure callback.
/// </summary>
/// <remarks>
/// The handler stamps the SDK's canonical <c>wt:*</c> claims (mirrored in <see cref="TestClaimTypes"/>) so code that
/// reads identity via the SDK's <c>ClaimsPrincipalExtensions</c> (<c>GetUserId()</c>, <c>GetEmail()</c>, …) works
/// unchanged under test. Add anything provider-specific via <see cref="ExtraClaims"/>.
/// </remarks>
public sealed class TestAuthOptions : AuthenticationSchemeOptions
{
    /// <summary>Value stamped as <c>wt:user_id</c> — the provider-stable user id. Default <c>"test-user"</c>.</summary>
    public string UserId { get; set; } = "test-user";

    /// <summary>Value stamped as the principal's <see cref="ClaimTypes.Name"/> (and <c>wt:display_name</c> when set). Default <c>"Test User"</c>.</summary>
    public string Name { get; set; } = "Test User";

    /// <summary>Value stamped as <c>wt:email</c>, when set. Default <c>null</c> (claim omitted).</summary>
    public string? Email { get; set; }

    /// <summary>Value stamped as <c>wt:provider</c> (the originating auth scheme). Default <c>"Test"</c>.</summary>
    public string Provider { get; set; } = "Test";

    /// <summary>Roles to stamp as <see cref="ClaimTypes.Role"/> claims — drives <c>[Authorize(Roles = …)]</c> / policy checks. Default empty.</summary>
    public IReadOnlyList<string> Roles { get; set; } = [];

    /// <summary>Extra raw claims appended verbatim — for provider-specific or app-specific claim types. Default empty.</summary>
    public IReadOnlyList<Claim> ExtraClaims { get; set; } = [];
}
