using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.AspNetCore.Http;

namespace WoW.Two.Sdk.Backend.Beta.Realtime.WebSockets;

/// <summary>
/// <see cref="HttpContext"/> and <see cref="WebSocket"/> helpers for raw-WebSocket endpoints — the
/// accept/close handshake, a text receive loop that reassembles fragmented messages, and a UTF-8 send.
/// </summary>
public static class WebSocketAcceptExtensions
{
    /// <summary>
    /// Accepts the WebSocket handshake and runs <paramref name="handler"/> for the connection's lifetime,
    /// performing a graceful close afterwards and swallowing client-disconnect cancellation. Responds
    /// <c>400</c> and returns <see langword="false"/> when the request is not a WebSocket upgrade.
    /// </summary>
    /// <param name="httpContext">The current request context.</param>
    /// <param name="handler">The handler that drives the socket.</param>
    /// <returns><see langword="true"/> when a socket was accepted and handled; otherwise <see langword="false"/>.</returns>
    public static async Task<bool> AcceptWebSocketAsync(this HttpContext httpContext, IWebSocketConnectionHandler handler)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(handler);

        if (!httpContext.WebSockets.IsWebSocketRequest)
        {
            httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            return false;
        }

        using var socket = await httpContext.WebSockets.AcceptWebSocketAsync();

        try
        {
            await handler.HandleAsync(socket, httpContext, httpContext.RequestAborted);
        }
        catch (OperationCanceledException) when (httpContext.RequestAborted.IsCancellationRequested)
        {
            // Client disconnected — fall through to the closing handshake.
        }

        if (socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
        {
            try
            {
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "closing", CancellationToken.None);
            }
            catch (WebSocketException)
            {
                // Socket already torn down by the peer — nothing to close.
            }
        }

        return true;
    }

    /// <summary>
    /// Reads complete UTF-8 text messages from the socket, reassembling fragments, until the peer closes
    /// or the token fires. Binary frames are ignored.
    /// </summary>
    /// <param name="socket">The open WebSocket to read from.</param>
    /// <param name="bufferSize">The receive buffer size in bytes. Default 4096.</param>
    /// <param name="cancellationToken">Stops the loop; typically the request-aborted token.</param>
    /// <returns>An async sequence of complete text messages in arrival order.</returns>
    public static async IAsyncEnumerable<string> ReceiveTextMessagesAsync(
        this WebSocket socket,
        int bufferSize = 4096,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(socket);

        var buffer = new byte[bufferSize];
        var message = new StringBuilder();

        while (socket.State == WebSocketState.Open)
        {
            WebSocketReceiveResult? result = null;
            try
            {
                result = await socket.ReceiveAsync(buffer, cancellationToken);
            }
            catch (WebSocketException)
            {
                yield break;
            }
            catch (OperationCanceledException)
            {
                yield break;
            }

            if (result.MessageType == WebSocketMessageType.Close)
                yield break;

            if (result.MessageType != WebSocketMessageType.Text)
                continue;

            message.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));

            if (result.EndOfMessage)
            {
                yield return message.ToString();
                message.Clear();
            }
        }
    }

    /// <summary>Sends <paramref name="message"/> as a single UTF-8 text frame.</summary>
    /// <param name="socket">The open WebSocket to write to.</param>
    /// <param name="message">The text to send.</param>
    /// <param name="cancellationToken">Token to abort the send.</param>
    public static async Task SendTextAsync(this WebSocket socket, string message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(socket);
        ArgumentNullException.ThrowIfNull(message);

        var bytes = Encoding.UTF8.GetBytes(message);
        await socket.SendAsync(bytes, WebSocketMessageType.Text, endOfMessage: true, cancellationToken);
    }
}
