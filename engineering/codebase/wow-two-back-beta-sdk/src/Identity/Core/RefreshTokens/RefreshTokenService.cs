using System.Buffers.Text;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Core.RefreshTokens;

/// <summary>
/// Provides rotating refresh tokens (OAuth 2.0 security BCP): each redemption consumes the token and issues its
/// replacement in the same family; presenting a consumed token again revokes the family. Tokens are stored by SHA-256
/// digest and die with a security-stamp rotation. Clients must serialize refreshes, or a race counts as reuse.
/// </summary>
/// <typeparam name="TUser">The user entity.</typeparam>
/// <typeparam name="TKey">The primary-key type.</typeparam>
/// <param name="tokens">The refresh-token repository.</param>
/// <param name="accounts">The core account service.</param>
/// <param name="options">The token lifetime.</param>
/// <param name="timeProvider">The clock issuance and expiry use.</param>
/// <param name="logger">Receives the reuse event.</param>
public sealed class RefreshTokenService<TUser, TKey>(
    IRefreshTokenRepository<TKey> tokens,
    UserAccountService<TUser, TKey> accounts,
    RefreshTokenOptions options,
    TimeProvider timeProvider,
    ILogger<RefreshTokenService<TUser, TKey>> logger)
    where TUser : IdentityUser<TKey>
    where TKey : notnull, IEquatable<TKey>
{
    private const int SecretLength = 32;

    /// <summary>Issue a token starting a new family, typically at sign-in.</summary>
    /// <param name="user">The signed-in user.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<RefreshTokenModel> IssueAsync(TUser user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        return IssueInFamilyAsync(user, Guid.CreateVersion7(), cancellationToken);
    }

    /// <summary>Redeem a token: on success it is spent and the result carries its replacement.</summary>
    /// <param name="token">The token the client presented.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<RefreshTokenResult<TUser>> RedeemAsync(string token, CancellationToken cancellationToken = default)
    {
        var row = await FindVerifiedAsync(token, cancellationToken);
        if (row is null || row.RevokedAt is not null)
            return Outcome(RefreshTokenStatus.Invalid);

        var now = timeProvider.GetUtcNow();
        if (row.ConsumedAt is not null)
            return await RevokeAsReusedAsync(row, now, cancellationToken);
        if (row.ExpiresAt <= now)
            return Outcome(RefreshTokenStatus.Expired);

        var user = await accounts.FindByIdAsync(row.UserId, cancellationToken);
        if (user is null || !string.Equals(user.SecurityStamp, row.SecurityStamp, StringComparison.Ordinal))
        {
            await tokens.RevokeFamilyAsync(row.FamilyId, now, cancellationToken);
            return Outcome(RefreshTokenStatus.Invalid);
        }

        if (!await tokens.TryConsumeAsync(row.Id, now, cancellationToken))
            return await RevokeAsReusedAsync(row, now, cancellationToken);

        return new RefreshTokenResult<TUser>
        {
            Status = RefreshTokenStatus.Succeeded,
            User = user,
            Token = await IssueInFamilyAsync(user, row.FamilyId, cancellationToken),
        };
    }

    /// <summary>Revoke the token's family, as at sign-out; an unknown token is ignored.</summary>
    /// <param name="token">The token to revoke.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task RevokeAsync(string token, CancellationToken cancellationToken = default)
    {
        if (await FindVerifiedAsync(token, cancellationToken) is { } row)
            await tokens.RevokeFamilyAsync(row.FamilyId, timeProvider.GetUtcNow(), cancellationToken);
    }

    /// <summary>Revoke every refresh token of <paramref name="user"/>, on every device.</summary>
    /// <param name="user">The user.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task RevokeAllAsync(TUser user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        return tokens.RevokeUserAsync(user.Id, timeProvider.GetUtcNow(), cancellationToken);
    }

    /// <summary>Delete tokens that expired before now; schedule it from a recurring job.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<int> PurgeExpiredAsync(CancellationToken cancellationToken = default)
        => tokens.DeleteExpiredAsync(timeProvider.GetUtcNow(), cancellationToken);

    private async Task<RefreshTokenModel> IssueInFamilyAsync(TUser user, Guid familyId, CancellationToken cancellationToken)
    {
        var secret = RandomNumberGenerator.GetBytes(SecretLength);
        var now = timeProvider.GetUtcNow();
        var row = new IdentityRefreshToken<TKey>
        {
            Id = Guid.CreateVersion7(now),
            UserId = user.Id,
            FamilyId = familyId,
            TokenHash = Convert.ToHexString(SHA256.HashData(secret)),
            SecurityStamp = user.SecurityStamp,
            CreatedAt = now,
            ExpiresAt = now + options.Lifetime,
        };
        await tokens.AddAsync(row, cancellationToken);
        return new RefreshTokenModel { Token = $"{row.Id:N}.{Base64Url.EncodeToString(secret)}", ExpiresAt = row.ExpiresAt };
    }

    private async Task<IdentityRefreshToken<TKey>?> FindVerifiedAsync(string? token, CancellationToken cancellationToken)
    {
        var separator = token?.IndexOf('.', StringComparison.Ordinal) ?? -1;
        if (separator != 32 || !Guid.TryParseExact(token.AsSpan(0, separator), "N", out var id))
            return null;

        Span<byte> secret = stackalloc byte[SecretLength];
        if (!TryDecode(token.AsSpan(separator + 1), secret))
            return null;

        var digest = SHA256.HashData(secret);
        var row = await tokens.FindAsync(id, cancellationToken);
        return row is not null && CryptographicOperations.FixedTimeEquals(digest, Convert.FromHexString(row.TokenHash))
            ? row
            : null;
    }

    private async Task<RefreshTokenResult<TUser>> RevokeAsReusedAsync(IdentityRefreshToken<TKey> row, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await tokens.RevokeFamilyAsync(row.FamilyId, now, cancellationToken);
        logger.RefreshTokenReused(row.FamilyId);
        return Outcome(RefreshTokenStatus.Reused);
    }

    private static bool TryDecode(ReadOnlySpan<char> source, Span<byte> destination)
    {
        try
        {
            return Base64Url.TryDecodeFromChars(source, destination, out var written) && written == destination.Length;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static RefreshTokenResult<TUser> Outcome(RefreshTokenStatus status) => new() { Status = status };
}
