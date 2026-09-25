using System.Data.Common;
using System.Diagnostics.Metrics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using WoW.Two.Sdk.Backend.Beta.Data.Abstractions;

namespace WoW.Two.Sdk.Backend.Beta.Data.Sessions;

/// <summary>Coordinates explicit EF transactions, raw SQL leases and completion callbacks.</summary>
/// <typeparam name="TContext">The primary relational context.</typeparam>
internal sealed class EfDataSession<TContext> : IDataSession where TContext : DbContext
{
    private static readonly Meter Meter = new("WoW.Two.Sdk.Backend.Beta.Data.Sessions");
    private static readonly Counter<long> FailedHooks = Meter.CreateCounter<long>("data.session.hook.failures");
    private readonly TContext _context;
    private readonly DataSessionOptions _options;
    private readonly ILogger<EfDataSession<TContext>> _logger;
    private readonly List<DataUnit> _units = [];
    private IDbContextTransaction? _transaction;
    private string? _connectionString;
    private int _leases;
    private int _busy;
    private int _sequence;
    private bool _commitOutcomeUnknown;

    public EfDataSession(
        TContext context,
        DataSessionOptions options,
        ILogger<EfDataSession<TContext>> logger)
    {
        _context = context;
        _options = options;
        _logger = logger;
        if (!context.Database.IsRelational())
        {
            throw new InvalidOperationException("Data sessions require a relational provider.");
        }
        if (context.Database.CreateExecutionStrategy().RetriesOnFailure)
        {
            throw new InvalidOperationException("Data sessions cannot use an automatically retrying execution strategy.");
        }
    }

    public DataSessionState State { get; private set; }
    public int Depth => _units.Count;

    public async ValueTask<IDataUnit> BeginAsync(CancellationToken cancellationToken = default)
    {
        Enter();
        try
        {
            EnsureUsable();
            if (_leases != 0 || Depth >= _options.MaxDepth)
            {
                throw new InvalidOperationException("Dispose connection leases and remain within the configured unit depth.");
            }

            string? savepoint = null;
            if (State == DataSessionState.Idle)
            {
                if (_context.Database.CurrentTransaction is not null
                    || System.Transactions.Transaction.Current is not null)
                {
                    throw new InvalidOperationException("A data session cannot adopt an external or ambient transaction.");
                }
                _connectionString = _context.Database.GetDbConnection().ConnectionString;
                _transaction = await _context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
                State = DataSessionState.Active;
            }
            else
            {
                EnsureTransactionOwnership();
                if (!_transaction!.SupportsSavepoints)
                {
                    throw new InvalidOperationException("The provider does not support nested data units.");
                }
                await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                savepoint = "sdk_unit_" + (++_sequence).ToString(System.Globalization.CultureInfo.InvariantCulture);
                await _transaction.CreateSavepointAsync(savepoint, cancellationToken).ConfigureAwait(false);
            }

            var unit = new DataUnit(CompleteAsync, AbandonAsync) { Savepoint = savepoint };
            _units.Add(unit);
            return unit;
        }
        finally
        {
            Exit();
        }
    }

    public async ValueTask<DataConnectionLease> OpenConnectionAsync(
        IDbConnectionFactory factory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(factory);
        cancellationToken.ThrowIfCancellationRequested();
        Enter();
        try
        {
            EnsureUsable();
            if (State == DataSessionState.Idle)
            {
                DbConnection owned = await factory.CreateOpenAsync(cancellationToken).ConfigureAwait(false);
                return new DataConnectionLease(owned, null, null);
            }

            EnsureTransactionOwnership();
            DbConnection connection = _context.Database.GetDbConnection();
            await using DbConnection candidate = factory.Create();
            if (candidate.GetType() != connection.GetType()
                || !SameConnectionTarget(connection, candidate.ConnectionString, _connectionString!))
            {
                throw new InvalidOperationException("The connection factory does not target the data session's database.");
            }
            if (_leases != 0)
            {
                throw new InvalidOperationException("Data-session connections cannot be leased concurrently.");
            }
            _leases++;
            return new DataConnectionLease(connection, _transaction!.GetDbTransaction(), () => _leases--);
        }
        finally
        {
            Exit();
        }
    }

    public async ValueTask OnCommittedAsync(
        Func<CancellationToken, ValueTask> action,
        string? dedupKey = null)
    {
        ArgumentNullException.ThrowIfNull(action);
        Enter();
        try
        {
            if (State is DataSessionState.Idle or DataSessionState.Committed)
            {
                await DrainAsync([action]).ConfigureAwait(false);
                return;
            }
            EnsureUsable();
            if (dedupKey is null || !_units.Any(unit => unit.CommitHooks.Any(hook => hook.Key == dedupKey)))
            {
                _units[^1].CommitHooks.Add(new DataSessionHook { Action = action, Key = dedupKey });
            }
        }
        finally
        {
            Exit();
        }
    }

    public void OnRolledBack(Func<CancellationToken, ValueTask> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        Enter();
        try
        {
            EnsureUsable();
            if (Depth == 0)
            {
                throw new InvalidOperationException("Rollback callbacks require an active data unit.");
            }
            _units[^1].RollbackHooks.Add(action);
        }
        finally
        {
            Exit();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (State == DataSessionState.Disposed)
        {
            return;
        }
        Enter();
        try
        {
            if (_commitOutcomeUnknown)
            {
                await CleanupUnknownCommitAsync().ConfigureAwait(false);
                return;
            }
            if (_transaction is not null)
            {
                using var cleanup = new CancellationTokenSource(_options.RollbackTimeout);
                try
                {
                    await _transaction.RollbackAsync(cleanup.Token).ConfigureAwait(false);
                }
                finally
                {
                    _context.ChangeTracker.Clear();
                    await DisposeTransactionAsync().ConfigureAwait(false);
                    foreach (DataUnit unit in _units)
                    {
                        unit.Settled = true;
                    }
                    await DrainAsync(_units.SelectMany(unit => unit.RollbackHooks).ToArray()).ConfigureAwait(false);
                    _units.Clear();
                }
            }
        }
        finally
        {
            State = DataSessionState.Disposed;
            Exit();
        }
    }

    private async ValueTask CompleteAsync(DataUnit unit, CancellationToken cancellationToken)
    {
        Enter();
        try
        {
            EnsureTop(unit);
            cancellationToken.ThrowIfCancellationRequested();
            EnsureTransactionOwnership();
            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (unit.Savepoint is not null)
            {
                await _transaction!.ReleaseSavepointAsync(unit.Savepoint, cancellationToken).ConfigureAwait(false);
                _units.RemoveAt(Depth - 1);
                unit.Settled = true;
                _units[^1].CommitHooks.AddRange(unit.CommitHooks);
                _units[^1].RollbackHooks.AddRange(unit.RollbackHooks);
                return;
            }

            try
            {
                await _transaction!.CommitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                _commitOutcomeUnknown = true;
                State = DataSessionState.Faulted;
                throw;
            }
            State = DataSessionState.Committed;
            unit.Settled = true;
            _units.Clear();
            await DisposeTransactionAsync().ConfigureAwait(false);
            await DrainAsync(unit.CommitHooks.Select(hook => hook.Action).ToArray()).ConfigureAwait(false);
        }
        finally
        {
            Exit();
        }
    }

    private async ValueTask AbandonAsync(DataUnit unit)
    {
        Enter();
        try
        {
            EnsureTop(unit, allowFaulted: true);
            if (_commitOutcomeUnknown)
            {
                await CleanupUnknownCommitAsync().ConfigureAwait(false);
                return;
            }
            using var cleanup = new CancellationTokenSource(_options.RollbackTimeout);
            try
            {
                if (unit.Savepoint is not null && State != DataSessionState.Faulted)
                {
                    await _transaction!.RollbackToSavepointAsync(unit.Savepoint, cleanup.Token).ConfigureAwait(false);
                    await _transaction.ReleaseSavepointAsync(unit.Savepoint, cleanup.Token).ConfigureAwait(false);
                }
                else
                {
                    await _transaction!.RollbackAsync(cleanup.Token).ConfigureAwait(false);
                    State = DataSessionState.RolledBack;
                    await DisposeTransactionAsync().ConfigureAwait(false);
                }
            }
            catch
            {
                State = DataSessionState.Faulted;
                throw;
            }
            finally
            {
                // The database rollback does not restore entity snapshots or store-generated concurrency values.
                _context.ChangeTracker.Clear();
            }
            if (State == DataSessionState.RolledBack)
            {
                var actions = _units.SelectMany(frame => frame.RollbackHooks).ToArray();
                foreach (DataUnit frame in _units)
                {
                    frame.Settled = true;
                }
                _units.Clear();
                await DrainAsync(actions).ConfigureAwait(false);
            }
            else
            {
                unit.Settled = true;
                _units.RemoveAt(Depth - 1);
                await DrainAsync(unit.RollbackHooks).ConfigureAwait(false);
            }
        }
        finally
        {
            Exit();
        }
    }

    private async ValueTask DisposeTransactionAsync()
    {
        IDbContextTransaction? transaction = _transaction;
        _transaction = null;
        if (transaction is null)
        {
            return;
        }
        try
        {
            await transaction.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Data-session transaction cleanup failed in state {State}", State);
        }
    }

    private async ValueTask CleanupUnknownCommitAsync()
    {
        using var cleanup = new CancellationTokenSource(_options.RollbackTimeout);
        try
        {
            if (_transaction is not null)
            {
                await _transaction.RollbackAsync(cleanup.Token).ConfigureAwait(false);
            }
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Cleanup could not confirm rollback after an uncertain commit; reconcile before retrying");
        }
        finally
        {
            _context.ChangeTracker.Clear();
            await DisposeTransactionAsync().ConfigureAwait(false);
            foreach (DataUnit unit in _units)
            {
                unit.Settled = true;
            }
            _units.Clear();
        }
    }

    private async ValueTask DrainAsync(IReadOnlyList<Func<CancellationToken, ValueTask>> actions)
    {
        for (int index = 0; index < actions.Count; index++)
        {
            using var budget = new CancellationTokenSource(_options.HookTimeout);
            try
            {
                budget.Token.ThrowIfCancellationRequested();
                await actions[index](budget.Token).AsTask().WaitAsync(budget.Token).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                FailedHooks.Add(1);
                _logger.LogError(
                    exception,
                    "Data-session callback failed; completed {Completed} of {Total}, state {State}",
                    index,
                    actions.Count,
                    State);
            }
        }
    }

    private void EnsureTop(DataUnit unit, bool allowFaulted = false)
    {
        if (State != DataSessionState.Active && !(allowFaulted && State == DataSessionState.Faulted)
            || Depth == 0 || !ReferenceEquals(_units[^1], unit))
        {
            throw new InvalidOperationException("Data units must settle in reverse opening order while active.");
        }
        if (_leases != 0)
        {
            throw new InvalidOperationException("Dispose all connection leases before settling a data unit.");
        }
    }

    private void EnsureTransactionOwnership()
    {
        if (!ReferenceEquals(_context.Database.CurrentTransaction, _transaction))
        {
            _commitOutcomeUnknown = true;
            State = DataSessionState.Faulted;
            throw new InvalidOperationException("The data-session transaction was changed outside its owning unit.");
        }
    }

    private void EnsureUsable()
    {
        if (State is not (DataSessionState.Idle or DataSessionState.Active))
        {
            throw new InvalidOperationException($"The data session is {State}; create a new scope.");
        }
    }

    private void Enter()
    {
        if (Interlocked.CompareExchange(ref _busy, 1, 0) != 0)
        {
            throw new InvalidOperationException("Data-session operations must be sequential.");
        }
    }

    private void Exit()
    {
        Volatile.Write(ref _busy, 0);
    }

    private static bool SameConnectionTarget(DbConnection connection, string left, string right)
    {
        DbProviderFactory? provider = DbProviderFactories.GetFactory(connection);
        DbConnectionStringBuilder a = provider?.CreateConnectionStringBuilder() ?? new DbConnectionStringBuilder();
        DbConnectionStringBuilder b = provider?.CreateConnectionStringBuilder() ?? new DbConnectionStringBuilder();
        a.ConnectionString = left;
        b.ConnectionString = right;
        // Pooled sources redact passwords; borrow EF credentials while requiring all other settings, including principal, to match.
        a.Remove("Password");
        a.Remove("Pwd");
        b.Remove("Password");
        b.Remove("Pwd");
        return a.EquivalentTo(b);
    }
}
