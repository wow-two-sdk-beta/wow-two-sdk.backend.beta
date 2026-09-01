using Microsoft.AspNetCore.Builder;

namespace WoW.Two.Sdk.Backend.Beta.Web.SecureHeaders;

/// <summary>Provides OWASP secure-headers middleware presets.</summary>
public static class SecureHeadersExtensions
{
    /// <summary>Uses a hardened API secure-headers preset (HSTS, nosniff, frame-deny, referrer, permissions, and cross-origin policies).</summary>
    /// <param name="app">The application request pipeline.</param>
    /// <param name="configure">Optional configuration; switches the cross-origin opener and embedder policies off.</param>
    public static IApplicationBuilder UseOwaspSecureHeaders(this IApplicationBuilder app, Action<SecureHeadersOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(app);

        var options = new SecureHeadersOptions();
        configure?.Invoke(options);

        return app.UseSecurityHeaders(policies =>
        {
            policies
                .AddDefaultSecurityHeaders()
                .AddStrictTransportSecurityMaxAgeIncludeSubDomains(maxAgeInSeconds: 60 * 60 * 24 * 365)
                .AddContentTypeOptionsNoSniff()
                .AddFrameOptionsDeny()
                .AddReferrerPolicyStrictOriginWhenCrossOrigin()
                .AddCrossOriginOpenerPolicy(b => b.SameOrigin())
                .AddCrossOriginEmbedderPolicy(b => b.RequireCorp())
                .AddCrossOriginResourcePolicy(b => b.SameOrigin())
                .RemoveServerHeader();

            // AddDefaultSecurityHeaders seeds both policies, so an opt-out removes the entry rather than skipping it.
            if (!options.EnableCrossOriginOpenerPolicy)
            {
                policies.Remove("Cross-Origin-Opener-Policy");
            }

            if (!options.EnableCrossOriginEmbedderPolicy)
            {
                policies.Remove("Cross-Origin-Embedder-Policy");
            }
        });
    }
}
