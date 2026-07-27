using System.Net;
using System.Net.Sockets;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Webhooks;

/// <summary>Thrown by the guarded connect callback when a webhook target resolves only to blocked (private / loopback / link-local) addresses.</summary>
internal sealed class WebhookAddressBlockedException(string host)
    : Exception($"Webhook target host '{host}' resolves only to blocked (private/loopback/link-local) addresses.")
{
    /// <summary>The rejected host.</summary>
    public string Host { get; } = host;
}

/// <summary>
/// SSRF guard for outbound webhook delivery: rejects targets that resolve to private, loopback, link-local, unique-local,
/// CGNAT, or multicast ranges (e.g. the cloud metadata endpoint <c>169.254.169.254</c>), and enforces scheme / host-allowlist
/// policy. <see cref="GuardedConnectAsync"/> validates the <em>actual</em> address being connected to — not just the pre-DNS
/// hostname — so it also defeats DNS-rebinding (a public name that resolves to a private IP).
/// </summary>
internal static class WebhookAddressPolicy
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
        var ip = address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;
        if (IPAddress.IsLoopback(ip) || ip.Equals(IPAddress.Any) || ip.Equals(IPAddress.IPv6Any))
            return true;

        var bytes = ip.GetAddressBytes();
        if (ip.AddressFamily == AddressFamily.InterNetwork)
        {
            return bytes[0] == 0                                  // 0.0.0.0/8 "this network"
                || bytes[0] == 10                                 // 10.0.0.0/8 private
                || (bytes[0] == 100 && (bytes[1] & 0xC0) == 64)   // 100.64.0.0/10 CGNAT
                || bytes[0] == 127                                // 127.0.0.0/8 loopback
                || (bytes[0] == 169 && bytes[1] == 254)           // 169.254.0.0/16 link-local (cloud metadata)
                || (bytes[0] == 172 && (bytes[1] & 0xF0) == 16)   // 172.16.0.0/12 private
                || (bytes[0] == 192 && bytes[1] == 168)           // 192.168.0.0/16 private
                || bytes[0] >= 224;                               // 224.0.0.0/4 multicast + 240.0.0.0/4 reserved
        }

        return ip.IsIPv6LinkLocal
            || ip.IsIPv6SiteLocal
            || ip.IsIPv6Multicast
            || (bytes[0] & 0xFE) == 0xFC;                         // fc00::/7 unique-local
    }

    /// <summary>A <see cref="System.Net.Http.SocketsHttpHandler"/> connect callback that resolves the target and connects only to non-blocked addresses; throws <see cref="WebhookAddressBlockedException"/> when none remain.</summary>
    /// <param name="context">The connection context (target host + port).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async ValueTask<Stream> GuardedConnectAsync(
        System.Net.Http.SocketsHttpConnectionContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var endpoint = context.DnsEndPoint;
        var addresses = await Dns.GetHostAddressesAsync(endpoint.Host, cancellationToken);
        var permitted = Array.FindAll(addresses, static a => !IsBlocked(a));
        if (permitted.Length == 0)
            throw new WebhookAddressBlockedException(endpoint.Host);

        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        try
        {
            await socket.ConnectAsync(permitted, endpoint.Port, cancellationToken);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }
}
