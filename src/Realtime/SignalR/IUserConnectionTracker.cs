namespace WoW.Two.Sdk.Backend.Beta.Realtime.SignalR;

/// <summary>
/// Tracks which SignalR connection ids belong to which authenticated user, so an application can answer
/// "is this user online?" and target every device a user has open. The default implementation is
/// in-memory and therefore <b>per-node</b>: behind a scale-out backplane it reflects only the
/// connections on the local server. Cross-node presence needs a distributed store.
/// </summary>
public interface IUserConnectionTracker
{
    /// <summary>Records that <paramref name="connectionId"/> belongs to <paramref name="userId"/>. Idempotent.</summary>
    /// <param name="userId">The authenticated user identifier (typically <c>Context.UserIdentifier</c>).</param>
    /// <param name="connectionId">The SignalR connection id.</param>
    void Track(string userId, string connectionId);

    /// <summary>Removes the association of <paramref name="connectionId"/> with <paramref name="userId"/>. Idempotent.</summary>
    /// <param name="userId">The authenticated user identifier.</param>
    /// <param name="connectionId">The SignalR connection id.</param>
    void Untrack(string userId, string connectionId);

    /// <summary>Returns the connection ids currently tracked for <paramref name="userId"/>; empty when the user is offline.</summary>
    /// <param name="userId">The authenticated user identifier.</param>
    /// <returns>A snapshot of the user's live connection ids.</returns>
    IReadOnlyCollection<string> GetConnections(string userId);

    /// <summary>Returns whether <paramref name="userId"/> has at least one live connection on this node.</summary>
    /// <param name="userId">The authenticated user identifier.</param>
    /// <returns><see langword="true"/> when the user has a tracked connection.</returns>
    bool IsOnline(string userId);

    /// <summary>Gets a snapshot of every user id with at least one live connection on this node.</summary>
    IReadOnlyCollection<string> OnlineUsers { get; }
}
