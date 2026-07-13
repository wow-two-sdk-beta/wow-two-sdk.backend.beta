using Microsoft.AspNetCore.SignalR;

namespace WoW.Two.Sdk.Backend.Beta.Realtime.SignalR;

/// <summary>
/// Base class for untyped SignalR hubs adding small conveniences over <see cref="Hub"/>:
/// the authenticated <see cref="UserId"/> and the current <see cref="ConnectionId"/>.
/// </summary>
public abstract class SdkHub : Hub
{
    /// <summary>Gets the authenticated user identifier for the calling connection, or <see langword="null"/> when anonymous.</summary>
    protected string? UserId => Context.UserIdentifier;

    /// <summary>Gets the SignalR connection id of the calling connection.</summary>
    protected string ConnectionId => Context.ConnectionId;
}

/// <summary>
/// Base class for strongly-typed SignalR hubs whose client methods are described by
/// <typeparamref name="TClient"/>, adding the same <see cref="UserId"/>/<see cref="ConnectionId"/>
/// conveniences over <see cref="Hub{T}"/>.
/// </summary>
/// <typeparam name="TClient">The strongly-typed client interface used to invoke client methods.</typeparam>
public abstract class SdkHub<TClient> : Hub<TClient>
    where TClient : class
{
    /// <summary>Gets the authenticated user identifier for the calling connection, or <see langword="null"/> when anonymous.</summary>
    protected string? UserId => Context.UserIdentifier;

    /// <summary>Gets the SignalR connection id of the calling connection.</summary>
    protected string ConnectionId => Context.ConnectionId;
}
