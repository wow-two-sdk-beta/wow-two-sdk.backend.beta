namespace WoW.Two.Sdk.Backend.Beta.Messaging.Webhooks;

/// <summary>
/// Pre-flight scheme and host-allowlist policy for outbound webhook delivery. Connect-time address blocking — private,
/// loopback, link-local, unique-local, CGNAT, multicast and transition ranges, including the cloud metadata endpoint
/// <c>169.254.169.254</c> — comes from the shared outbound HTTP safety callback, which validates the address actually
/// dialled rather than the pre-DNS hostname, so it also defeats DNS rebinding.
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
}
