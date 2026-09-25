using System.Data.Common;

namespace WoW.Two.Sdk.Backend.Beta.Data.Sessions;

/// <summary>Owns an independent connection or borrows a session connection until disposal.</summary>
public sealed class DataConnectionLease : IAsyncDisposable
{
    private readonly DbConnection _connection;
    private readonly DbTransaction? _transaction;
    private readonly Action? _release;
    private bool _disposed;

    internal DataConnectionLease(DbConnection connection, DbTransaction? transaction, Action? release)
    {
        _connection = connection;
        _transaction = transaction;
        _release = release;
    }

    /// <summary>Gets the connection while the lease is alive.</summary>
    public DbConnection Connection
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _connection;
        }
    }

    /// <summary>Gets the transaction to pass to every raw SQL command.</summary>
    public DbTransaction? Transaction
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _transaction;
        }
    }

    /// <summary>Gets whether disposal closes the connection.</summary>
    public bool OwnsConnection => _release is null;

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        if (_release is not null)
        {
            _release();
        }
        else
        {
            await _connection.DisposeAsync().ConfigureAwait(false);
        }
    }
}
