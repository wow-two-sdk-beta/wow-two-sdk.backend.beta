namespace WoW.Two.Sdk.Backend.Beta.Identity.Core.Passkeys;

/// <summary>Defines the seam reaching stored passkeys.</summary>
/// <typeparam name="TKey">User key type.</typeparam>
public interface IUserPasskeyRepository<TKey>
    where TKey : notnull, IEquatable<TKey>
{
    /// <summary>Stores a new passkey.</summary>
    /// <param name="passkey">The passkey.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task AddAsync(IdentityPasskey<TKey> passkey, CancellationToken cancellationToken = default);

    /// <summary>The passkey with <paramref name="credentialId"/>, or null.</summary>
    /// <param name="credentialId">The credential id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IdentityPasskey<TKey>?> FindAsync(byte[] credentialId, CancellationToken cancellationToken = default);

    /// <summary>The user's passkeys, oldest first.</summary>
    /// <param name="userId">The user.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<IdentityPasskey<TKey>>> ListAsync(TKey userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a sign-in — the new counter, and <paramref name="usedAt"/> as the last use — only while the last use is
    /// earlier than <paramref name="ceremonyIssuedAt"/>, as one atomic write. False when a use at or after the ceremony's
    /// start is already recorded: that ceremony, or an older one, was replayed.
    /// </summary>
    /// <param name="id">The passkey.</param>
    /// <param name="signCount">The authenticator's counter.</param>
    /// <param name="ceremonyIssuedAt">When the sign-in ceremony started.</param>
    /// <param name="usedAt">The last use to record; never earlier than <paramref name="ceremonyIssuedAt"/>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<bool> RecordUseAsync(Guid id, uint signCount, DateTimeOffset ceremonyIssuedAt, DateTimeOffset usedAt, CancellationToken cancellationToken = default);

    /// <summary>Deletes one of the user's passkeys; false when the user has no such passkey.</summary>
    /// <param name="userId">The user.</param>
    /// <param name="id">The passkey.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<bool> RemoveAsync(TKey userId, Guid id, CancellationToken cancellationToken = default);
}
