using Fido2NetLib;
using Microsoft.Extensions.DependencyInjection;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Mfa.WebAuthn;

/// <summary>WebAuthn / FIDO2 registration helpers.</summary>
public static class WebAuthnServiceCollectionExtensions
{
    /// <summary>Registers <c>Fido2</c> services for WebAuthn ceremonies (registration and assertion).</summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="serverDomain">Relying-party domain (e.g. `example.com`). Must match the browser origin.</param>
    /// <param name="serverName">Relying-party display name shown to the user.</param>
    /// <param name="origins">Allowed origins for browser ceremonies (e.g. `https://example.com`).</param>
    public static IServiceCollection AddFido2WebAuthn(
        this IServiceCollection services,
        string serverDomain,
        string serverName,
        params string[] origins)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(serverDomain);
        ArgumentException.ThrowIfNullOrWhiteSpace(serverName);
        ArgumentNullException.ThrowIfNull(origins);

        services.AddFido2(o =>
        {
            o.ServerDomain = serverDomain;
            o.ServerName = serverName;
            o.Origins = new HashSet<string>(origins);
            o.TimestampDriftTolerance = 300_000;
        });

        return services;
    }
}
