using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.OAuth;

namespace WoW.Two.Sdk.Backend.Beta.Identity.OAuth;

/// <summary>Uniform OAuth baseline: persist tokens, merge scopes, stamp the originating scheme as the <c>wt:provider</c> claim.</summary>
internal static class OAuthBaseline
{
    /// <summary>Claim type carrying the originating scheme name; read by <c>Identity/Claims</c> via <c>NormalizedClaimTypes.Provider</c>.</summary>
    public const string ProviderClaimType = "wt:provider";

    /// <summary>Pre-<c>configure</c> half: enable <c>SaveTokens</c> and merge scopes. Call before the host's <c>configure</c>.</summary>
    /// <param name="options">Provider options being configured.</param>
    /// <param name="scopes">Extra scopes to request; duplicates skipped.</param>
    public static void ApplyBaseline(OAuthOptions options, params string[] scopes)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.SaveTokens = true;

        if (scopes is { Length: > 0 })
        {
            foreach (var scope in scopes)
            {
                if (!string.IsNullOrWhiteSpace(scope) && !options.Scope.Contains(scope))
                {
                    options.Scope.Add(scope);
                }
            }
        }
    }

    /// <summary>Post-<c>configure</c> half: wrap <see cref="OAuthEvents.OnCreatingTicket"/> so the <c>wt:provider</c> stamp runs last and cannot be dropped. Call after the host's <c>configure</c>.</summary>
    /// <param name="options">Provider options being configured.</param>
    public static void StampProvider(OAuthOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var inner = options.Events.OnCreatingTicket;
        options.Events.OnCreatingTicket = async context =>
        {
            await inner(context).ConfigureAwait(false);
            context.Identity?.AddClaim(new Claim(ProviderClaimType, context.Scheme.Name));
        };
    }
}
