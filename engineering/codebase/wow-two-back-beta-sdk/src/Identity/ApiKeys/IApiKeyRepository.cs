namespace WoW.Two.Sdk.Backend.Beta.Identity.ApiKeys;

/// <summary>Defines the product's repository of API keys, which the scheme reads by hash — secrets are never stored.</summary>
/// <remarks>Register it scoped, beside <c>AddApiKeyAuthentication</c>; the scheme resolves it per request.</remarks>
public interface IApiKeyRepository
{
    /// <summary>Finds the live — unrevoked, unexpired — key with a secret hash.</summary>
    /// <param name="hash">The lowercase hex SHA-256 of the presented secret.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The key, or <c>null</c> when no live key has the hash.</returns>
    Task<ApiKeyRecord?> FindLiveByHashAsync(string hash, CancellationToken cancellationToken);

    /// <summary>Records that a key authenticated a request; called at most once per touch interval per key.</summary>
    /// <param name="id">The key's identifier.</param>
    /// <param name="usedAt">The instant of use.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that completes when the use is recorded.</returns>
    Task TouchAsync(string id, DateTimeOffset usedAt, CancellationToken cancellationToken);
}
