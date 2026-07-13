using System.Collections.Concurrent;

namespace WoW.Two.Sdk.Backend.Beta.Realtime.SignalR;

/// <summary>
/// Thread-safe, in-process <see cref="IUserConnectionTracker"/> backed by a concurrent map of
/// user id to connection-id set. Scope is the local node only — pair with a scale-out backplane for
/// message fan-out, and a distributed store if you need cluster-wide presence.
/// </summary>
public sealed class InMemoryUserConnectionTracker : IUserConnectionTracker
{
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> _byUser = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public void Track(string userId, string connectionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionId);

        var connections = _byUser.GetOrAdd(userId, static _ => new ConcurrentDictionary<string, byte>(StringComparer.Ordinal));
        connections[connectionId] = 0;
    }

    /// <inheritdoc />
    public void Untrack(string userId, string connectionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionId);

        if (!_byUser.TryGetValue(userId, out var connections)) return;

        connections.TryRemove(connectionId, out _);

        // Drop the empty bucket, then guard against a concurrent Track that re-added between the emptiness
        // check and the removal.
        if (connections.IsEmpty && _byUser.TryRemove(userId, out var removed) && !removed.IsEmpty)
            foreach (var kept in removed) _byUser.GetOrAdd(userId, static _ => new ConcurrentDictionary<string, byte>(StringComparer.Ordinal))[kept.Key] = 0;
    }

    /// <inheritdoc />
    public IReadOnlyCollection<string> GetConnections(string userId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        return _byUser.TryGetValue(userId, out var connections) ? connections.Keys.ToArray() : [];
    }

    /// <inheritdoc />
    public bool IsOnline(string userId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        return _byUser.TryGetValue(userId, out var connections) && !connections.IsEmpty;
    }

    /// <inheritdoc />
    public IReadOnlyCollection<string> OnlineUsers => _byUser.Keys.ToArray();
}
