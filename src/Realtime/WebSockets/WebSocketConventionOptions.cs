namespace WoW.Two.Sdk.Backend.Beta.Realtime.WebSockets;

/// <summary>Conventional options for the raw-WebSocket middleware applied by <see cref="WebSocketConventions"/>.</summary>
public sealed class WebSocketConventionOptions
{
    /// <summary>Gets or sets the interval at which keep-alive frames are sent to hold idle connections open. Default 15 seconds.</summary>
    public TimeSpan KeepAliveInterval { get; set; } = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Gets the allowed <c>Origin</c> header values for the WebSocket handshake. Empty (the default)
    /// accepts any origin — populate it to restrict cross-site WebSocket connections.
    /// </summary>
    public IList<string> AllowedOrigins { get; } = [];
}
