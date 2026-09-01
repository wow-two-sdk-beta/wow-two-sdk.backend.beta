using System.Net;
using System.Net.Sockets;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Webhooks;

/// <summary>Connects only to addresses the webhook rules permit, resolving the host first so the check sees the
/// address actually dialled.</summary>
internal sealed class WebhookSsrfGuard
{
    /// <summary>A <see cref="System.Net.Http.SocketsHttpHandler"/> connect callback that resolves the target and connects only to non-blocked addresses; throws <see cref="WebhookAddressBlockedException"/> when none remain.</summary>
    /// <param name="context">The connection context (target host + port).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async ValueTask<Stream> GuardedConnectAsync(
        System.Net.Http.SocketsHttpConnectionContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var endpoint = context.DnsEndPoint;
        var addresses = await Dns.GetHostAddressesAsync(endpoint.Host, cancellationToken);
        var permitted = Array.FindAll(addresses, static a => !WebhookAddressMapper.IsBlocked(a));
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
    }}
