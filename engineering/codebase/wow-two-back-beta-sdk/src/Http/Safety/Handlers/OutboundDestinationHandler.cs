using System.Net;

namespace WoW.Two.Sdk.Backend.Beta.Http.Safety.Handlers;

/// <summary>Intercepts outbound requests to enforce scheme, host and transport restrictions.</summary>
internal sealed class OutboundDestinationHandler(OutboundHttpOptions options) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        Uri? uri = request.RequestUri;
        if (uri is null || !uri.IsAbsoluteUri
            || uri.UserInfo.Length != 0
            || uri.Scheme != Uri.UriSchemeHttps && (options.RequireHttps || uri.Scheme != Uri.UriSchemeHttp)
            || options.AllowedHosts.Count != 0 && !options.AllowedHosts.Contains(uri.IdnHost.TrimEnd('.')))
        {
            throw new OutboundAddressBlockedException(uri?.IsAbsoluteUri == true ? uri.IdnHost : "(relative)");
        }

        // QUIC does not use SocketsHttpHandler.ConnectCallback.
        if (request.Version.Major >= 3 || request.VersionPolicy == HttpVersionPolicy.RequestVersionOrHigher)
        {
            throw new InvalidOperationException("Guarded outbound HTTP requires HTTP/1 or HTTP/2 without version upgrades.");
        }

        return base.SendAsync(request, cancellationToken);
    }
}
