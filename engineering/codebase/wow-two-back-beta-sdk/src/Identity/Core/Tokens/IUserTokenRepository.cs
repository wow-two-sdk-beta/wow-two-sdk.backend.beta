namespace WoW.Two.Sdk.Backend.Beta.Identity.Core.Tokens;

/// <summary>Defines stored user tokens: named values per user and provider (authenticator key, recovery codes, provider tokens).</summary>
/// <typeparam name="TKey">The primary-key type.</typeparam>
public interface IUserTokenRepository<TKey>
    where TKey : notnull, IEquatable<TKey>
{
    /// <summary>The stored value, or null.</summary>
    /// <param name="userId">The user id.</param>
    /// <param name="loginProvider">The provider the value belongs to.</param>
    /// <param name="name">The value's name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<string?> GetAsync(TKey userId, string loginProvider, string name, CancellationToken cancellationToken = default);

    /// <summary>Store or overwrite a value.</summary>
    /// <param name="userId">The user id.</param>
    /// <param name="loginProvider">The provider the value belongs to.</param>
    /// <param name="name">The value's name.</param>
    /// <param name="value">The value.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SetAsync(TKey userId, string loginProvider, string name, string value, CancellationToken cancellationToken = default);

    /// <summary>Remove a value when present.</summary>
    /// <param name="userId">The user id.</param>
    /// <param name="loginProvider">The provider the value belongs to.</param>
    /// <param name="name">The value's name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RemoveAsync(TKey userId, string loginProvider, string name, CancellationToken cancellationToken = default);
}
