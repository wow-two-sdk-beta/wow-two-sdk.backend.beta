using Microsoft.AspNetCore.Builder;

namespace WoW.Two.Sdk.Backend.Beta.Web.SecureHeaders;

/// <summary>Provides OWASP secure-headers middleware presets.</summary>
public static class SecureHeadersExtensions
{
    /// <summary>Uses a hardened API secure-headers preset (HSTS, nosniff, frame-deny, referrer, permissions, and cross-origin policies).</summary>
    /// <param name="app">The application request pipeline.</param>
    public static IApplicationBuilder UseOwaspSecureHeaders(this IApplicationBuilder app)
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
