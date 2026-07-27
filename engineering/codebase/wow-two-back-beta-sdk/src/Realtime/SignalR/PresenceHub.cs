namespace WoW.Two.Sdk.Backend.Beta.Realtime.SignalR;

/// <summary>
/// A <see cref="SdkHub"/> that automatically registers and unregisters the connection with an
/// <see cref="IUserConnectionTracker"/> on connect/disconnect, giving presence and multi-device
/// targeting for free. Derive your hub from this instead of <see cref="SdkHub"/> to opt in; override
/// <see cref="OnConnectedAsync"/>/<see cref="OnDisconnectedAsync"/> and call <c>base</c> to add behaviour.
/// </summary>
public abstract class PresenceHub : SdkHub
{
    private readonly IUserConnectionTracker _tracker;

    /// <summary>Creates the hub with the connection tracker resolved from DI.</summary>
    /// <param name="tracker">The tracker recording the user's live connections.</param>
    protected PresenceHub(IUserConnectionTracker tracker)
    {
        ArgumentNullException.ThrowIfNull(tracker);
        _tracker = tracker;
    }

    /// <inheritdoc />
    public override async Task OnConnectedAsync()
    {
        if (UserId is { } userId)
            _tracker.Track(userId, ConnectionId);

        await base.OnConnectedAsync();
    }

    /// <inheritdoc />
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (UserId is { } userId)
            _tracker.Untrack(userId, ConnectionId);

        await base.OnDisconnectedAsync(exception);
    }
}
