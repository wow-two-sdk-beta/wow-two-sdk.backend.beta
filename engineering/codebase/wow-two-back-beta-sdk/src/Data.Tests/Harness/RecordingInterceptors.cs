using System.Collections.Concurrent;
using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace WoW.Two.Sdk.Backend.Beta.Data.Tests.Harness;

/// <summary>The shared ordered log every recording interceptor appends to — invocation order is the assertion for the ordering cases.</summary>
public sealed class InterceptorLog
{
    private readonly ConcurrentQueue<string> _entries = new();

    /// <summary>Every recorded entry, in invocation order.</summary>
    public IReadOnlyList<string> Entries => [.. _entries];

    /// <summary>Records one invocation.</summary>
    /// <param name="entry">The entry to append.</param>
    public void Add(string entry) => _entries.Enqueue(entry);

    /// <summary>How many entries carry the given value.</summary>
    /// <param name="entry">The entry to count.</param>
    public int CountOf(string entry) => _entries.Count(existing => existing == entry);
}

/// <summary>Base for the suite's save-recording interceptors — appends <see cref="Tag"/> to the shared log on every save.</summary>
/// <param name="log">The shared invocation log.</param>
public abstract class RecordingSaveChangesInterceptorBase(InterceptorLog log) : SaveChangesInterceptor
{
    /// <summary>The value this interceptor appends to the log.</summary>
    protected abstract string Tag { get; }

    /// <summary>The shared invocation log.</summary>
    protected InterceptorLog Log { get; } = log;

    /// <inheritdoc />
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Log.Add(Tag);
        return base.SavingChanges(eventData, result);
    }

    /// <inheritdoc />
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Log.Add(Tag);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}

/// <summary>The first pluggable interceptor — registered via <c>AddEfInterceptor</c>, so its firing proves the auto-wire loop ran.</summary>
/// <param name="log">The shared invocation log.</param>
public sealed class FirstRecordingInterceptor(InterceptorLog log) : RecordingSaveChangesInterceptorBase(log)
{
    /// <summary>The log entry this interceptor writes.</summary>
    public const string Name = "first";

    /// <inheritdoc />
    protected override string Tag => Name;
}

/// <summary>The second pluggable interceptor — its position relative to <see cref="FirstRecordingInterceptor"/> is the ordering assertion.</summary>
/// <param name="log">The shared invocation log.</param>
public sealed class SecondRecordingInterceptor(InterceptorLog log) : RecordingSaveChangesInterceptorBase(log)
{
    /// <summary>The log entry this interceptor writes.</summary>
    public const string Name = "second";

    /// <inheritdoc />
    protected override string Tag => Name;
}

/// <summary>
/// Records the transaction/savepoint event stream and, alongside it, the depth a synthetic frame would carry if EF's
/// automatic per-<c>SaveChanges</c> savepoint events never arrived.
/// </summary>
/// <remarks>
/// <para>PR1 is a fork, so this records both branches at once. EF's transaction event payload carries no savepoint
/// name, so "automatic" is discriminated by <em>when</em> the event arrives: EF creates its own savepoint inside a
/// <c>SaveChanges</c> (between <c>SavingChanges</c> and <c>SavedChanges</c>), while a caller's
/// <c>CreateSavepointAsync</c> lands outside any save.</para>
/// <para><see cref="SyntheticDepthPerSave"/> counts only caller-created savepoints, so it yields identical depth tags
/// on both branches of the fork — that equivalence is the fallback's contract.</para>
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

    // ── Transaction events ──

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

    // ── Savepoint events — the PR1 fork ──

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

    // ── Save events — the window that discriminates EF's savepoints from the caller's, and the synthetic frame ──

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
        // The synthetic frame is opened here, before EF gets a chance to create its own savepoint — so the tag never
        // depends on whether that savepoint raises an event.
        _syntheticDepthPerSave.Add(_transactionOpen ? _explicitSavepoints + 1 : 0);
        _insideSave = true;
    }

    private void OnSavepointCreated()
    {
        _events.Add(Label("created"));

        if (_insideSave)
        {
            AutomaticSavepointsCreated++;
            return; // deliberately NOT counted into depth: doing so would make the two branches of the fork disagree
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

    private string Label(string verb) => $"{verb}:{(_insideSave ? "auto" : "explicit")}";
}
