using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace WoW.Two.Sdk.Backend.Beta.Testing.Auth;

/// <summary>
/// Authentication handler that authenticates a request as the fixed identity in <see cref="TestAuthOptions"/> —
/// no real OAuth / cookie round-trip. Register it via <c>AddTestAuth</c> from a test host so end-to-end tests can
/// exercise authenticated endpoints deterministically.
/// </summary>
/// <remarks>
///   - authenticates every request while <see cref="TestAuthOptions.RequiredHeader"/> is <c>null</c>, the default
///   - with it set, a request missing that header yields <see cref="AuthenticateResult.NoResult"/>, so <c>[Authorize]</c> returns 401
///   - stamps <c>wt:*</c> claims plus <c>ClaimTypes.Name</c> / <c>Role</c>, so SDK and framework role checks resolve
/// </remarks>
public sealed class TestAuthHandler : AuthenticationHandler<TestAuthOptions>
{
    /// <summary>The scheme name this handler is registered under by <c>AddTestAuth</c>.</summary>
    public const string SchemeName = "Test";

    /// <summary>Creates the handler. Resolved by the auth framework — not called directly.</summary>
    /// <param name="options">Monitor over the configured <see cref="TestAuthOptions"/>.</param>
    /// <param name="logger">Logger factory supplied by the framework.</param>
    /// <param name="encoder">URL encoder supplied by the framework.</param>
    public TestAuthHandler(
        IOptionsMonitor<TestAuthOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    /// <inheritdoc />
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var o = Options;

        // Required header absent — stay anonymous so [Authorize] endpoints return 401.
        if (!string.IsNullOrEmpty(o.RequiredHeader) && !Request.Headers.ContainsKey(o.RequiredHeader))
            return Task.FromResult(AuthenticateResult.NoResult());

        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, o.Name),
            new(ClaimTypes.NameIdentifier, o.UserId),
            new(TestClaimTypeConstants.UserId, o.UserId),
            new(TestClaimTypeConstants.DisplayName, o.Name),
            new(TestClaimTypeConstants.Provider, o.Provider),
        };

        if (!string.IsNullOrEmpty(o.Email))
            claims.Add(new Claim(TestClaimTypeConstants.Email, o.Email));

        foreach (var role in o.Roles)
            claims.Add(new Claim(ClaimTypes.Role, role));

        claims.AddRange(o.ExtraClaims);

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
