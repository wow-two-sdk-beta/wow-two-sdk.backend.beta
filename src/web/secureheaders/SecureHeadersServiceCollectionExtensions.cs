using Microsoft.AspNetCore.Builder;

namespace WoW.Two.Sdk.Backend.Beta.Web.SecureHeaders;

/// <summary>
/// OWASP secure-headers middleware presets.
/// </summary>
public static class SecureHeadersExtensions
{
    /// <summary>
    /// Apply a hardened secure-headers preset suitable for APIs:
    /// - HSTS (1 year, preload, includeSubDomains)
    /// - X-Content-Type-Options: nosniff
    /// - X-Frame-Options: DENY
    /// - Referrer-Policy: strict-origin-when-cross-origin
    /// - Permissions-Policy: minimal
    /// - Cross-Origin-{Opener,Embedder,Resource}-Policy
    /// </summary>
    public static IApplicationBuilder UseWowTwoSecureHeaders(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        return app.UseSecurityHeaders(policies => policies
            .AddDefaultSecurityHeaders()
            .AddStrictTransportSecurityMaxAgeIncludeSubDomains(maxAgeInSeconds: 60 * 60 * 24 * 365)
            .AddContentTypeOptionsNoSniff()
            .AddFrameOptionsDeny()
            .AddReferrerPolicyStrictOriginWhenCrossOrigin()
            .AddCrossOriginOpenerPolicy(b => b.SameOrigin())
            .AddCrossOriginEmbedderPolicy(b => b.RequireCorp())
            .AddCrossOriginResourcePolicy(b => b.SameOrigin())
            .RemoveServerHeader());
    }
}
