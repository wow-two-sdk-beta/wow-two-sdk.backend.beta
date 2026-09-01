using System.Collections.Concurrent;
using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace WoW.Two.Sdk.Backend.Beta.Data.Tests.Harness;

/// <summary>
/// Records the transaction/savepoint event stream and, alongside it, the depth a synthetic frame would carry if EF's
/// automatic per-<c>SaveChanges</c> savepoint events never arrived.
/// </summary>
/// <remarks>
///   - a savepoint reads as automatic when it lands inside a <c>SaveChanges</c>
///   - a caller's <c>CreateSavepointAsync</c> lands outside any save, so it reads as explicit
///   - <see cref="SyntheticDepthPerSave"/> counts caller-created savepoints only
/// </remarks>
public sealed class SavepointFrameRecorder : DbTransactionInterceptor, ISaveChangesInterceptor
{
    private readonly List<string> _events = [];
    private readonly List<int> _syntheticDepthPerSave = [];
    private int _explicitSavepoints;
    private bool _transactionOpen;
    private bool _insideSave;

    /// <summary>Every transaction/savepoint event, in order.</summary>
    public IReadOnlyList<string> Events => _events;

    /// <summary>Savepoints EF created on its own, inside a <c>SaveChanges</c>, that actually raised <c>CreatedSavepoint</c>.</summary>
    public int AutomaticSavepointsCreated { get; private set; }

    /// <summary>Savepoints EF rolled back to on its own, inside a <c>SaveChanges</c>, that actually raised <c>RolledBackToSavepoint</c>.</summary>
    public int AutomaticSavepointRollbacks { get; private set; }

    /// <summary>Caller-created savepoints that raised <c>CreatedSavepoint</c>.</summary>
    public int ExplicitSavepointsCreated { get; private set; }

    /// <summary>The frame depth each <c>SaveChanges</c> would be tagged at by the synthetic fallback — <c>0</c> outside a transaction, <c>1</c> inside a bare unit, <c>2</c> inside one caller savepoint, and so on.</summary>
    public IReadOnlyList<int> SyntheticDepthPerSave => _syntheticDepthPerSave;

    /// <inheritdoc />
    public override DbTransaction TransactionStarted(DbConnection connection, TransactionEndEventData eventData, DbTransaction result)
    {
        OnTransactionStarted();
        return base.TransactionStarted(connection, eventData, result);
    }

    /// <inheritdoc />
    public override ValueTask<DbTransaction> TransactionStartedAsync(DbConnection connection, TransactionEndEventData eventData, DbTransaction result, CancellationToken cancellationToken = default)
    {
        OnTransactionStarted();
        return base.TransactionStartedAsync(connection, eventData, result, cancellationToken);
    }

    /// <inheritdoc />
    public override void TransactionCommitted(DbTransaction transaction, TransactionEndEventData eventData)
    {
        OnTransactionEnded("committed");
        base.TransactionCommitted(transaction, eventData);
    }

    /// <inheritdoc />
    public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        OnTransactionEnded("committed");
        return base.TransactionCommittedAsync(transaction, eventData, cancellationToken);
    }

    /// <inheritdoc />
    public override void TransactionRolledBack(DbTransaction transaction, TransactionEndEventData eventData)
    {
        OnTransactionEnded("rolledback");
        base.TransactionRolledBack(transaction, eventData);
    }

    /// <inheritdoc />
    public override Task TransactionRolledBackAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        OnTransactionEnded("rolledback");
        return base.TransactionRolledBackAsync(transaction, eventData, cancellationToken);
    }

    /// <inheritdoc />
    public override void CreatedSavepoint(DbTransaction transaction, TransactionEventData eventData)
    {
        OnSavepointCreated();
        base.CreatedSavepoint(transaction, eventData);
    }

    /// <inheritdoc />
    public override Task CreatedSavepointAsync(DbTransaction transaction, TransactionEventData eventData, CancellationToken cancellationToken = default)
    {
        OnSavepointCreated();
        return base.CreatedSavepointAsync(transaction, eventData, cancellationToken);
    }

    /// <inheritdoc />
    public override void RolledBackToSavepoint(DbTransaction transaction, TransactionEventData eventData)
    {
        OnSavepointRolledBackTo();
        base.RolledBackToSavepoint(transaction, eventData);
    }

    /// <inheritdoc />
    public override Task RolledBackToSavepointAsync(DbTransaction transaction, TransactionEventData eventData, CancellationToken cancellationToken = default)
    {
        OnSavepointRolledBackTo();
        return base.RolledBackToSavepointAsync(transaction, eventData, cancellationToken);
    }

    /// <inheritdoc />
    public override void ReleasedSavepoint(DbTransaction transaction, TransactionEventData eventData)
    {
        _events.Add(Label("released"));
        base.ReleasedSavepoint(transaction, eventData);
    }

    /// <inheritdoc />
    public override Task ReleasedSavepointAsync(DbTransaction transaction, TransactionEventData eventData, CancellationToken cancellationToken = default)
    {
        _events.Add(Label("released"));
        return base.ReleasedSavepointAsync(transaction, eventData, cancellationToken);
    }

    InterceptionResult<int> ISaveChangesInterceptor.SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        OnSaveStarting();
        return result;
    }

    ValueTask<InterceptionResult<int>> ISaveChangesInterceptor.SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken)
    {
        OnSaveStarting();
        return ValueTask.FromResult(result);
    }

    int ISaveChangesInterceptor.SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        _insideSave = false;
        return result;
    }

    ValueTask<int> ISaveChangesInterceptor.SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken)
    {
        _insideSave = false;
        return ValueTask.FromResult(result);
    }

    void ISaveChangesInterceptor.SaveChangesFailed(DbContextErrorEventData eventData) => _insideSave = false;

    Task ISaveChangesInterceptor.SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken)
    {
        _insideSave = false;
        return Task.CompletedTask;
    }

    private void OnTransactionStarted()
    {
        _events.Add("started");
        _transactionOpen = true;
        _explicitSavepoints = 0;
    }

    private void OnTransactionEnded(string verb)
    {
        _events.Add(verb);
        _transactionOpen = false;
        _explicitSavepoints = 0;
    }

    private void OnSaveStarting()
    {
        // Opens the synthetic frame before EF can create its own savepoint, so the tag never depends on that event.
        _syntheticDepthPerSave.Add(_transactionOpen ? _explicitSavepoints + 1 : 0);
        _insideSave = true;
    }

    private void OnSavepointCreated()
    {
        _events.Add(Label("created"));

        if (_insideSave)
        {
            AutomaticSavepointsCreated++;
            return; // an automatic savepoint leaves the synthetic depth unchanged
        }

        ExplicitSavepointsCreated++;
        _explicitSavepoints++;
    }

    private void OnSavepointRolledBackTo()
    {
        _events.Add(Label("rolledback-to"));

        // Rolling back to a savepoint does not release it — frame depth is unchanged, matching the design's frame model.
        if (_insideSave)
            AutomaticSavepointRollbacks++;
    }

    // EF's transaction event payload carries no savepoint name, so save timing is the only discriminator.
    private string Label(string verb) => $"{verb}:{(_insideSave ? "auto" : "explicit")}";
}
