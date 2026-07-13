using System.Text.Json;

namespace WoW.Two.Sdk.Backend.Beta.Realtime.SignalR;

/// <summary>
/// Conventional SignalR hub tunables applied by
/// <see cref="SignalRConventions.AddConventionalSignalR"/>. Defaults favour responsive presence
/// detection (fast keep-alive + client timeout) and a bounded receive size to blunt abuse.
/// </summary>
public sealed record SignalRConventionOptions
{
    /// <summary>Gets the interval at which the server pings idle clients. Should be ≤ half the client timeout. Default 15 seconds.</summary>
    public TimeSpan KeepAliveInterval { get; init; } = TimeSpan.FromSeconds(15);

    /// <summary>Gets how long the server waits for any client message (including pings) before treating the connection as dropped. Default 30 seconds.</summary>
    public TimeSpan ClientTimeoutInterval { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>Gets the maximum time allowed for the connection handshake. Default 15 seconds.</summary>
    public TimeSpan HandshakeTimeout { get; init; } = TimeSpan.FromSeconds(15);

    /// <summary>Gets the maximum inbound message size in bytes; larger messages are rejected. <see langword="null"/> removes the cap. Default 64 KB.</summary>
    public long? MaximumReceiveMessageSize { get; init; } = 64 * 1024;

    /// <summary>Gets whether hub method exceptions are surfaced to clients. Leave <see langword="false"/> in production to avoid leaking internals. Default <see langword="false"/>.</summary>
    public bool EnableDetailedErrors { get; init; }

    /// <summary>Gets the JSON payload serializer options; the SDK wire preset (camelCase) is used when omitted. A mutable copy is taken so SignalR can attach its own converters.</summary>
    public JsonSerializerOptions? JsonOptions { get; init; }
}
