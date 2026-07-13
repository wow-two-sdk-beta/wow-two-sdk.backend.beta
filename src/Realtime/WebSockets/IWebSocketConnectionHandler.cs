using System.Net.WebSockets;
using Microsoft.AspNetCore.Http;

namespace WoW.Two.Sdk.Backend.Beta.Realtime.WebSockets;

/// <summary>
/// Handles an accepted raw WebSocket for its full lifetime. Implementations run the receive/send loop
/// and return when the socket should close; <see cref="WebSocketAcceptExtensions"/> performs the accept
/// handshake and the closing handshake around this call.
/// </summary>
public interface IWebSocketConnectionHandler
{
    /// <summary>
    /// Drives the connection until completion. Returning (or throwing) ends the connection; the caller
    /// closes the socket. Honour <paramref name="cancellationToken"/> — it fires when the client aborts.
    /// </summary>
    /// <param name="socket">The accepted, open WebSocket.</param>
    /// <param name="httpContext">The originating request context (auth, route values, query).</param>
    /// <param name="cancellationToken">Fires when the request is aborted (client disconnect).</param>
    Task HandleAsync(WebSocket socket, HttpContext httpContext, CancellationToken cancellationToken);
}
