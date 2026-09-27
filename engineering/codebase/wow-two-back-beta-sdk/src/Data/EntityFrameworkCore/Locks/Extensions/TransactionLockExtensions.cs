using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace WoW.Two.Sdk.Backend.Beta.Data.EntityFrameworkCore.Locks.Extensions;

/// <summary>Extends EF Core transactions with exclusive per-key locks held until the transaction ends.</summary>
public static class TransactionLockExtensions
{
    /// <summary>Acquires an exclusive lock for every key in the current transaction, waiting for competing holders.</summary>
    /// <param name="database">The context's database facade, with an active transaction.</param>
    /// <param name="keys">Lock keys, namespaced by subject such as <c>owner:{id}</c>.</param>
    /// <param name="cancellationToken">Cancels waiting for a contended lock.</param>
    /// <remarks>
    ///   - keys hash to 64 bits and lock in ascending order, so callers cannot deadlock on key order
    ///   - a hash collision serializes unrelated keys; it never weakens exclusion
    ///   - PostgreSQL releases the locks at commit or rollback; SQLite serializes writers and takes none
    ///   - every writer of a guarded invariant must lock the same keys, or the lock protects nothing
    /// </remarks>
    /// <exception cref="ArgumentException">A key is null or blank.</exception>
    /// <exception cref="InvalidOperationException">No transaction is active.</exception>
    /// <exception cref="NotSupportedException">The provider has no supported transaction-scoped lock.</exception>
    public static async Task AcquireTransactionLocksAsync(
        this DatabaseFacade database,
        IEnumerable<string> keys,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(keys);

        long[] hashes = keys.Select(Hash).Distinct().Order().ToArray();
        if (database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("Transaction locks require an active database transaction.");
        }

        if (database.IsSqlite())
        {
            return;
        }

        if (!database.IsNpgsql())
        {
            throw new NotSupportedException($"Transaction locks are not supported by provider '{database.ProviderName}'.");
        }

        foreach (long hash in hashes)
        {
            await database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock({hash})", cancellationToken)
                .ConfigureAwait(false);
        }
    }

    /// <summary>Acquires exclusive locks for identifiers within one subject, as keys <c>{subject}:{id}</c>.</summary>
    /// <param name="database">The context's database facade, with an active transaction.</param>
    /// <param name="subject">The subject naming the identifier space, such as <c>owner</c>.</param>
    /// <param name="ids">The identifiers to lock.</param>
    /// <param name="cancellationToken">Cancels waiting for a contended lock.</param>
    /// <exception cref="ArgumentException">The subject is null or blank.</exception>
    public static Task AcquireTransactionLocksAsync(
        this DatabaseFacade database,
        string subject,
        IEnumerable<Guid> ids,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);
        ArgumentNullException.ThrowIfNull(ids);

        return database.AcquireTransactionLocksAsync(ids.Select(id => $"{subject}:{id:N}"), cancellationToken);
    }

    private static long Hash(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return BinaryPrimitives.ReadInt64LittleEndian(SHA256.HashData(Encoding.UTF8.GetBytes(key)));
    }
}
