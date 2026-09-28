using System.ComponentModel;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Core.SecurityStamps;

/// <summary>
/// Validates that a principal's security stamp still equals the stored user's, so rotating the stamp revokes cookies and
/// bearer tokens. The stored stamp is cached per host for <see cref="SecurityStampValidationOptions.ValidationInterval"/>.
/// </summary>
/// <typeparam name="TUser">The user entity.</typeparam>
/// <typeparam name="TKey">The primary-key type.</typeparam>
/// <param name="repository">The core user store.</param>
/// <param name="identity">Identity options carrying the claim types.</param>
/// <param name="options">Interval and missing-stamp behavior.</param>
/// <param name="cache">Holds recently read stamps.</param>
/// <param name="logger">Receives the rejection event.</param>
public sealed class SecurityStampValidator<TUser, TKey>(
    IUserRepository<TUser, TKey> repository,
    IdentityCoreOptions identity,
    SecurityStampValidationOptions options,
    IMemoryCache cache,
    ILogger<SecurityStampValidator<TUser, TKey>> logger)
    where TUser : IdentityUser<TKey>
    where TKey : notnull, IEquatable<TKey>
{
    private static readonly TypeConverter KeyConverter = TypeDescriptor.GetConverter(typeof(TKey));

    /// <summary>Whether <paramref name="principal"/> may stay signed in. A principal without a user id is not ours and passes.</summary>
    /// <param name="principal">The authenticated principal.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<bool> ValidateAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(principal);

        var userId = principal.FindFirst(identity.Claims.UserIdClaimType)?.Value;
        if (string.IsNullOrEmpty(userId))
            return true;

        var presented = principal.FindFirst(identity.Claims.SecurityStampClaimType)?.Value;
        if (presented is null)
            return !options.RejectPrincipalsWithoutStamp;

        var stored = await ReadStampAsync(userId, cancellationToken);
        var valid = stored is not null
            && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(stored), Encoding.UTF8.GetBytes(presented));
        if (!valid)
            logger.SecurityStampRejected(userId);

        return valid;
    }

    private async Task<string?> ReadStampAsync(string userId, CancellationToken cancellationToken)
    {
        var cacheKey = $"identity:security-stamp:{typeof(TUser).FullName}:{userId}";
        if (options.ValidationInterval > TimeSpan.Zero && cache.TryGetValue(cacheKey, out string? cached))
            return cached;

        var stamp = TryParseKey(userId, out var key)
            ? (await repository.FindByIdAsync(key, cancellationToken))?.SecurityStamp
            : null;

        if (options.ValidationInterval > TimeSpan.Zero && stamp is not null)
            cache.Set(cacheKey, stamp, options.ValidationInterval);

        return stamp;
    }

    private static bool TryParseKey(string value, out TKey key)
    {
        try
        {
            key = (TKey)KeyConverter.ConvertFromInvariantString(value)!;
            return key is not null;
        }
        catch (Exception exception) when (exception is FormatException or NotSupportedException or ArgumentException)
        {
            key = default!;
            return false;
        }
    }
}
