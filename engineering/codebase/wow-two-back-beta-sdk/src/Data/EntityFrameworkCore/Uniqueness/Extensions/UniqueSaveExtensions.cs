using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace WoW.Two.Sdk.Backend.Beta.Data.EntityFrameworkCore.Uniqueness.Extensions;

/// <summary>Extends EF Core saves with unique-constraint conflicts reported as results inside a transaction.</summary>
public static class UniqueSaveExtensions
{
    private const string Savepoint = "sdk_unique_save";
    private const int SqliteConstraint = 19;
    private const int SqliteConstraintUnique = 2067;
    private const int SqliteConstraintPrimaryKey = 1555;

    /// <summary>Saves pending changes under a savepoint, reporting a unique-constraint conflict instead of throwing.</summary>
    /// <param name="context">The context, with an active transaction.</param>
    /// <param name="constraintName">The PostgreSQL constraint that counts as a conflict; <see langword="null"/> accepts any unique one.</param>
    /// <param name="cancellationToken">Cancels the save.</param>
    /// <returns><see langword="true"/> when saved; <see langword="false"/> when the constraint rejected the write.</returns>
    /// <remarks>
    ///   - a conflict rolls back to the savepoint, so the transaction stays usable
    ///   - entities the failed save was adding are detached; modified ones keep their pending values
    ///   - SQLite reports no constraint name, so there any unique violation counts
    /// </remarks>
    /// <exception cref="InvalidOperationException">No transaction is active.</exception>
    public static async Task<bool> TrySaveChangesUniqueAsync(
        this DbContext context,
        string? constraintName = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        IDbContextTransaction transaction = context.Database.CurrentTransaction
            ?? throw new InvalidOperationException("Unique saves require an active database transaction.");

        await transaction.CreateSavepointAsync(Savepoint, cancellationToken).ConfigureAwait(false);
        try
        {
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.ReleaseSavepointAsync(Savepoint, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception.InnerException, constraintName))
        {
            await transaction.RollbackToSavepointAsync(Savepoint, cancellationToken).ConfigureAwait(false);
            await transaction.ReleaseSavepointAsync(Savepoint, cancellationToken).ConfigureAwait(false);
            foreach (var entry in exception.Entries.Where(entry => entry.State == EntityState.Added))
            {
                entry.State = EntityState.Detached;
            }

            return false;
        }
    }

    /// <summary>Adds <paramref name="entity"/> with generated keys until one saves or the attempts run out.</summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <param name="context">The context, with an active transaction.</param>
    /// <param name="entity">The entity to insert.</param>
    /// <param name="applyKey">Writes a candidate key onto the entity.</param>
    /// <param name="nextKey">Produces the next random candidate key.</param>
    /// <param name="maxAttempts">The most candidates tried; bounded so a saturated key space cannot spin.</param>
    /// <param name="constraintName">The unique constraint on the key; <see langword="null"/> accepts any unique one.</param>
    /// <param name="cancellationToken">Cancels the save.</param>
    /// <returns>The saved key, or <see langword="null"/> when every attempt collided.</returns>
    public static async Task<string?> SaveWithGeneratedKeyAsync<TEntity>(
        this DbContext context,
        TEntity entity,
        Action<TEntity, string> applyKey,
        Func<string> nextKey,
        int maxAttempts,
        string? constraintName = null,
        CancellationToken cancellationToken = default)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentNullException.ThrowIfNull(applyKey);
        ArgumentNullException.ThrowIfNull(nextKey);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxAttempts, 1);

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string key = nextKey();
            applyKey(entity, key);
            context.Add(entity);
            if (await context.TrySaveChangesUniqueAsync(constraintName, cancellationToken).ConfigureAwait(false))
            {
                return key;
            }
        }

        return null;
    }

    private static bool IsUniqueViolation(Exception? cause, string? constraintName) => cause switch
    {
        PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } postgres =>
            constraintName is null || string.Equals(postgres.ConstraintName, constraintName, StringComparison.Ordinal),
        SqliteException { SqliteErrorCode: SqliteConstraint } sqlite =>
            sqlite.SqliteExtendedErrorCode is SqliteConstraintUnique or SqliteConstraintPrimaryKey,
        _ => false,
    };
}
