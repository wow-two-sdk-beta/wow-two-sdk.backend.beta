using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Backend.Beta.Http.Safety.Handlers;
using WoW.Two.Sdk.Backend.Beta.Http.Safety.Validators;

namespace WoW.Two.Sdk.Backend.Beta.Http.Safety;

/// <summary>Provides factory-managed HTTP clients with explicit destination protection.</summary>
public static class SafeOutboundHttpClientBuilderExtensions
{
    /// <summary>Restricts destinations and disables redirects, proxies and shared cookies.</summary>
    /// <remarks>
    /// Registers the primary transport. Do not replace it or enable redirects/proxies afterward.
    /// Resolve redirects explicitly through this client so every destination is checked.
    /// No HTTP/3 is permitted because its connections bypass the TCP callback.
    /// </remarks>
    /// <param name="builder">The named or typed client registration.</param>
    /// <param name="configure">Optional destination policy.</param>
    public static IHttpClientBuilder AddSafeOutboundHttp(
        this IHttpClientBuilder builder,
        Action<OutboundHttpOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        var options = new OutboundHttpOptions();
        configure?.Invoke(options);
        var snapshot = new OutboundHttpOptions
        {
            RequireHttps = options.RequireHttps,
            AllowPrivateNetworkTargets = options.AllowPrivateNetworkTargets
        };
        foreach (string host in options.AllowedHosts)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(host);
            if (Uri.CheckHostName(host.TrimEnd('.')) == UriHostNameType.Unknown)
            {
                throw new ArgumentException("Allowed hosts must be exact host names or IP literals.", nameof(configure));
            }
            snapshot.AllowedHosts.Add(new UriBuilder(Uri.UriSchemeHttps, host).Uri.IdnHost.TrimEnd('.'));
        }

        builder.ConfigurePrimaryHttpMessageHandler(() =>
        {
            var handler = new SocketsHttpHandler
            {
                AllowAutoRedirect = false,
                UseProxy = false,
                UseCookies = false,
                PooledConnectionLifetime = TimeSpan.FromMinutes(5)
            };
            if (!snapshot.AllowPrivateNetworkTargets)
            {
                handler.ConnectCallback = ConnectAsync;
            }
            return handler;
        });
        return builder.AddHttpMessageHandler(() => new OutboundDestinationHandler(snapshot));
    }

    private static async ValueTask<Stream> ConnectAsync(
        SocketsHttpConnectionContext context,
        CancellationToken cancellationToken)
    {
        DnsEndPoint endpoint = context.DnsEndPoint;
        IPAddress[] addresses = await Dns.GetHostAddressesAsync(endpoint.Host, cancellationToken).ConfigureAwait(false);
        var validator = new OutboundAddressValidator();
        IPAddress[] allowed = Array.FindAll(addresses, validator.IsAllowed);
        if (allowed.Length == 0)
        {
            throw new OutboundAddressBlockedException(endpoint.Host);
        }

        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        try
        {
            await socket.ConnectAsync(allowed, endpoint.Port, cancellationToken).ConfigureAwait(false);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }
}
