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

    /// <summary>
    /// Optional request header that gates authentication, letting one test host exercise both the authenticated
    /// path and the anonymous-gate (401) path. Default <c>null</c> — preserving the always-authenticate behavior.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description><c>null</c> (default): every request authenticates as this identity — back-compatible.</description></item>
    /// <item><description>Set (e.g. <c>"X-Test-Auth"</c>): only requests carrying this header (any value) authenticate;
    /// requests lacking it stay anonymous, so <see cref="TestAuthHandler.HandleAuthenticateAsync"/> returns
    /// <see cref="Microsoft.AspNetCore.Authentication.AuthenticateResult.NoResult"/> and <c>[Authorize]</c> endpoints
    /// challenge with 401.</description></item>
    /// </list>
    /// A test sends the header to assert the 200 path and omits it to assert the 401 path, from the same SDK helper.
    /// </remarks>
    public string? RequiredHeader { get; set; }
}
