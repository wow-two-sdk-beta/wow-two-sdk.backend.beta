using System.Security.Cryptography;
using System.Text;
using OtpNet;
using WoW.Two.Sdk.Backend.Beta.Identity.Core.Tokens;
using WoW.Two.Sdk.Backend.Beta.Identity.Mfa.Totp;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Core.TwoFactor;

/// <summary>
/// Provides the two-factor slice: an authenticator (TOTP) key per user, enable/disable with a verified code, and
/// single-use recovery codes stored as SHA-256 digests. Key and codes live in the stored-token table under the
/// ASP.NET Identity provider name, so migrated keys keep working.
/// </summary>
/// <typeparam name="TUser">The user entity.</typeparam>
/// <typeparam name="TKey">The primary-key type.</typeparam>
/// <param name="repository">The core user store.</param>
/// <param name="tokens">The stored-token repository.</param>
/// <param name="totp">Computes and verifies authenticator codes.</param>
/// <param name="options">Authenticator label and recovery-code count.</param>
public sealed class UserTwoFactorService<TUser, TKey>(
    IUserRepository<TUser, TKey> repository,
    IUserTokenRepository<TKey> tokens,
    ITotpService totp,
    TwoFactorOptions options)
    where TUser : IdentityUser<TKey>
    where TKey : notnull, IEquatable<TKey>
{
    /// <summary>The provider name ASP.NET Identity stores its own tokens under.</summary>
    public const string TokenProvider = "[AspNetUserStore]";

    private const string AuthenticatorKeyName = "AuthenticatorKey";
    private const string RecoveryCodesName = "RecoveryCodes";

    /// <summary>Generate and store a new authenticator key, rotating the stamp; the account must verify it before enabling.</summary>
    /// <param name="user">The user.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The base32 key to show or encode as a QR code.</returns>
    public async Task<string> ResetAuthenticatorKeyAsync(TUser user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        var key = totp.ToBase32(totp.GenerateSecret());
        await tokens.SetAsync(user.Id, TokenProvider, AuthenticatorKeyName, key, cancellationToken);
        user.RotateSecurityStamp();
        await repository.UpdateAsync(user, cancellationToken);
        return key;
    }

    /// <summary>The stored base32 authenticator key, or null before the first reset.</summary>
    /// <param name="user">The user.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<string?> GetAuthenticatorKeyAsync(TUser user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        return tokens.GetAsync(user.Id, TokenProvider, AuthenticatorKeyName, cancellationToken);
    }

    /// <summary>The <c>otpauth://</c> URI for the stored key, or null before the first reset.</summary>
    /// <param name="user">The user.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<Uri?> GetAuthenticatorUriAsync(TUser user, CancellationToken cancellationToken = default)
    {
        var key = await GetAuthenticatorKeyAsync(user, cancellationToken);
        return key is null
            ? null
            : totp.BuildOtpAuthUri(options.Issuer, user.Email ?? user.UserName ?? string.Empty, Base32Encoding.ToBytes(key));
    }

    /// <summary>Whether <paramref name="code"/> verifies against the stored authenticator key.</summary>
    /// <param name="user">The user.</param>
    /// <param name="code">The code from the authenticator app.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<bool> VerifyAuthenticatorCodeAsync(TUser user, string code, CancellationToken cancellationToken = default)
    {
        var key = await GetAuthenticatorKeyAsync(user, cancellationToken);
        return key is not null && !string.IsNullOrWhiteSpace(code) && totp.VerifyCode(Base32Encoding.ToBytes(key), code.Replace(" ", string.Empty, StringComparison.Ordinal));
    }

    /// <summary>Enable two-factor once <paramref name="code"/> proves the authenticator holds the stored key; rotates the stamp.</summary>
    /// <param name="user">The user.</param>
    /// <param name="code">A current authenticator code.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<IdentityResult> EnableAsync(TUser user, string code, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (await GetAuthenticatorKeyAsync(user, cancellationToken) is null)
            return IdentityUserExtensions.Failure(IdentityErrorCodeConstants.AuthenticatorNotConfigured, "Set up an authenticator before enabling two-factor.");
        if (!await VerifyAuthenticatorCodeAsync(user, code, cancellationToken))
            return IdentityUserExtensions.Failure(IdentityErrorCodeConstants.InvalidAuthenticatorCode, "The authenticator code is invalid.");

        user.TwoFactorEnabled = true;
        user.RotateSecurityStamp();
        await repository.UpdateAsync(user, cancellationToken);
        return IdentityResult.Success;
    }

    /// <summary>Disable two-factor and forget the key and recovery codes; rotates the stamp.</summary>
    /// <param name="user">The user.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<IdentityResult> DisableAsync(TUser user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        await tokens.RemoveAsync(user.Id, TokenProvider, AuthenticatorKeyName, cancellationToken);
        await tokens.RemoveAsync(user.Id, TokenProvider, RecoveryCodesName, cancellationToken);
        user.TwoFactorEnabled = false;
        user.RotateSecurityStamp();
        await repository.UpdateAsync(user, cancellationToken);
        return IdentityResult.Success;
    }

    /// <summary>Replace the recovery codes; the plain codes are returned once and only their digests are stored.</summary>
    /// <param name="user">The user.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<IReadOnlyList<string>> GenerateRecoveryCodesAsync(TUser user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        var codes = Enumerable.Range(0, options.RecoveryCodeCount).Select(_ => NewRecoveryCode()).ToList();
        await tokens.SetAsync(user.Id, TokenProvider, RecoveryCodesName, string.Join(';', codes.Select(Digest)), cancellationToken);
        return codes;
    }

    /// <summary>Consume one recovery code.</summary>
    /// <param name="user">The user.</param>
    /// <param name="code">The code, with or without its dash, in any case.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<IdentityResult> RedeemRecoveryCodeAsync(TUser user, string code, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        var digests = await ReadDigestsAsync(user, cancellationToken);
        var digest = string.IsNullOrWhiteSpace(code) ? null : Digest(code);
        if (digest is null || !digests.Remove(digest))
            return IdentityUserExtensions.Failure(IdentityErrorCodeConstants.InvalidRecoveryCode, "The recovery code is invalid or already used.");

        await tokens.SetAsync(user.Id, TokenProvider, RecoveryCodesName, string.Join(';', digests), cancellationToken);
        return IdentityResult.Success;
    }

    /// <summary>How many recovery codes remain unused.</summary>
    /// <param name="user">The user.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<int> CountRecoveryCodesAsync(TUser user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        return (await ReadDigestsAsync(user, cancellationToken)).Count;
    }

    private async Task<List<string>> ReadDigestsAsync(TUser user, CancellationToken cancellationToken)
    {
        var stored = await tokens.GetAsync(user.Id, TokenProvider, RecoveryCodesName, cancellationToken);
        return string.IsNullOrEmpty(stored) ? [] : [.. stored.Split(';', StringSplitOptions.RemoveEmptyEntries)];
    }

    private static string NewRecoveryCode()
    {
        var code = RandomNumberGenerator.GetString("ABCDEFGHJKLMNPQRSTUVWXYZ23456789", 10);
        return $"{code[..5]}-{code[5..]}";
    }

    private static string Digest(string code)
    {
        var canonical = code.Replace("-", string.Empty, StringComparison.Ordinal).Trim().ToUpperInvariant();
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }
}
