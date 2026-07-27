using Microsoft.AspNetCore.Builder;

namespace WoW.Two.Sdk.Backend.Beta.Realtime.WebSockets;

/// <summary>Registers the raw-WebSocket middleware with conventional keep-alive and origin defaults.</summary>
public static class WebSocketConventions
{
    /// <summary>
    /// Adds the WebSocket middleware configured from <see cref="WebSocketConventionOptions"/>. Call before
    /// the endpoints that accept sockets via <see cref="WebSocketAcceptExtensions"/>.
    /// </summary>
    /// <param name="app">The application pipeline to extend.</param>
    /// <param name="configure">Optional overrides for keep-alive interval and allowed origins.</param>
    /// <returns>The same <see cref="IApplicationBuilder"/> for chaining.</returns>
    public static IApplicationBuilder UseConventionalWebSockets(this IApplicationBuilder app, Action<WebSocketConventionOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(app);

        var options = new WebSocketConventionOptions();
        configure?.Invoke(options);

        var webSocketOptions = new WebSocketOptions { KeepAliveInterval = options.KeepAliveInterval };
        foreach (var origin in options.AllowedOrigins)
            webSocketOptions.AllowedOrigins.Add(origin);

        return app.UseWebSockets(webSocketOptions);
    }
}
