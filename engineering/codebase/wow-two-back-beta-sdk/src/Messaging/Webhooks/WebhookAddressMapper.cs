using System.Net;
using WoW.Two.Sdk.Backend.Beta.Http.Safety.Validators;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Webhooks;

/// <summary>
/// SSRF guard for outbound webhook delivery: rejects targets that resolve to private, loopback, link-local, unique-local,
/// CGNAT, or multicast ranges (e.g. the cloud metadata endpoint <c>169.254.169.254</c>), and enforces scheme / host-allowlist
/// policy. <see cref="WebhookSsrfGuard.GuardedConnectAsync"/> validates the <em>actual</em> address being connected to — not just the pre-DNS
/// hostname — so it also defeats DNS-rebinding (a public name that resolves to a private IP).
/// </summary>
internal static class WebhookAddressMapper
{
    /// <summary>True if <paramref name="url"/> uses an allowed scheme (https, or http too when <paramref name="requireHttps"/> is false).</summary>
    public static bool IsSchemeAllowed(Uri url, bool requireHttps)
        => requireHttps
            ? string.Equals(url.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal)
            : string.Equals(url.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal)
                || string.Equals(url.Scheme, Uri.UriSchemeHttp, StringComparison.Ordinal);

    /// <summary>True if <paramref name="url"/>'s host is permitted by <paramref name="allowlist"/> (empty allowlist = any host).</summary>
    public static bool IsHostAllowed(Uri url, ICollection<string> allowlist)
        => allowlist.Count == 0 || allowlist.Contains(url.Host);

    /// <summary>True if <paramref name="address"/> falls in a range a webhook must never reach (private / loopback / link-local / ULA / CGNAT / multicast / unspecified).</summary>
    public static bool IsBlocked(IPAddress address)
    {
        return !new OutboundAddressValidator().IsAllowed(address);
    }
}
