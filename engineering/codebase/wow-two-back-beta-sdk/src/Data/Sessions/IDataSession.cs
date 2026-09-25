using WoW.Two.Sdk.Backend.Beta.Data.Abstractions;

namespace WoW.Two.Sdk.Backend.Beta.Data.Sessions;

/// <summary>Defines a scoped transaction shared by EF writes and leased raw SQL connections.</summary>
public interface IDataSession : IAsyncDisposable
{
    /// <summary>Gets the current lifetime state.</summary>
    DataSessionState State { get; }

    /// <summary>Gets the number of open units.</summary>
    int Depth { get; }

    /// <summary>Begins the root transaction or a nested savepoint.</summary>
    /// <param name="cancellationToken">Cancels transaction setup.</param>
    ValueTask<IDataUnit> BeginAsync(CancellationToken cancellationToken = default);

    /// <summary>Opens an owned connection when idle or borrows the active EF connection and transaction.</summary>
    /// <param name="factory">The independent factory targeting the same database.</param>
    /// <param name="cancellationToken">Cancels connection acquisition.</param>
    ValueTask<DataConnectionLease> OpenConnectionAsync(
        IDbConnectionFactory factory,
        CancellationToken cancellationToken = default);

    /// <summary>Registers a commit action; idle or committed sessions invoke it immediately.</summary>
    /// <param name="action">An idempotent callback honoring its independent cancellation token.</param>
    /// <param name="dedupKey">An optional key retaining the shallowest surviving registration.</param>
    ValueTask OnCommittedAsync(Func<CancellationToken, ValueTask> action, string? dedupKey = null);

    /// <summary>Registers compensation in the current explicit unit.</summary>
    /// <param name="action">An idempotent callback honoring its independent cancellation token.</param>
    void OnRolledBack(Func<CancellationToken, ValueTask> action);
}
