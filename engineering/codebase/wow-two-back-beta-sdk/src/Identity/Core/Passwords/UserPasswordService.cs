using Microsoft.AspNetCore.Identity;
using WoW.Two.Sdk.Backend.Beta.Identity.Core.Tokens;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Core.Passwords;

/// <summary>
/// Provides the password slice: validate, set, change, remove and check a user's password, plus token-based reset when
/// purpose tokens are registered. Every stored password rotates the security stamp, so older sessions and tokens end.
/// </summary>
/// <typeparam name="TUser">The user entity.</typeparam>
/// <typeparam name="TKey">The primary-key type.</typeparam>
/// <param name="repository">The core user store.</param>
/// <param name="accounts">The core account service, for creation with a password.</param>
/// <param name="hasher">The password hasher; Argon2id unless the host registered another.</param>
/// <param name="validators">The registered password checks, run in registration order.</param>
/// <param name="tokens">The purpose-token issuer; null until <c>.AddUserTokens()</c> registers one.</param>
public sealed class UserPasswordService<TUser, TKey>(
    IUserRepository<TUser, TKey> repository,
    UserAccountService<TUser, TKey> accounts,
    IPasswordHasher<TUser> hasher,
    IEnumerable<IUserPasswordValidator<TUser>> validators,
    UserTokenIssuer<TUser, TKey>? tokens = null)
    where TUser : IdentityUser<TKey>
    where TKey : notnull, IEquatable<TKey>
{
    /// <summary>Whether <paramref name="user"/> has a password.</summary>
    /// <param name="user">The user to inspect.</param>
    public bool HasPassword(TUser user)
    {
        ArgumentNullException.ThrowIfNull(user);
        return !string.IsNullOrEmpty(user.PasswordHash);
    }

    /// <summary>Run every registered password check against <paramref name="password"/>.</summary>
    /// <param name="user">The account the password is for.</param>
    /// <param name="password">The candidate password.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<IdentityResult> ValidateAsync(TUser user, string password, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(password);

        var errors = new List<IdentityError>();
        foreach (var validator in validators)
            errors.AddRange(await validator.ValidateAsync(user, password, cancellationToken));

        return errors.Count == 0 ? IdentityResult.Success : IdentityResult.Failed([.. errors]);
    }

    /// <summary>Validate the password, then create the user with it; nothing persists when the password fails.</summary>
    /// <param name="user">The new user.</param>
    /// <param name="password">The initial password.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<IdentityResult> CreateWithPasswordAsync(TUser user, string password, CancellationToken cancellationToken = default)
    {
        var validation = await ValidateAsync(user, password, cancellationToken);
        if (!validation.Succeeded)
            return validation;

        user.PasswordHash = hasher.HashPassword(user, password);
        return await accounts.CreateAsync(user, cancellationToken);
    }

    /// <summary>Set the first password of an account that has none (for example after an external-login sign-up).</summary>
    /// <param name="user">The user.</param>
    /// <param name="password">The new password.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<IdentityResult> AddPasswordAsync(TUser user, string password, CancellationToken cancellationToken = default)
        => HasPassword(user)
            ? Task.FromResult(IdentityUserExtensions.Failure(IdentityErrorCodeConstants.UserAlreadyHasPassword, "The account already has a password."))
            : StoreAsync(user, password, cancellationToken);

    /// <summary>Replace the password after verifying the current one.</summary>
    /// <param name="user">The user.</param>
    /// <param name="currentPassword">The password the caller claims is current.</param>
    /// <param name="newPassword">The replacement password.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<IdentityResult> ChangePasswordAsync(TUser user, string currentPassword, string newPassword, CancellationToken cancellationToken = default)
        => Verify(user, currentPassword) == PasswordVerificationResult.Failed
            ? Task.FromResult(IdentityUserExtensions.Failure(IdentityErrorCodeConstants.PasswordMismatch, "The current password is incorrect."))
            : StoreAsync(user, newPassword, cancellationToken);

    /// <summary>Remove the password, leaving only the account's other sign-in methods.</summary>
    /// <param name="user">The user.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<IdentityResult> RemovePasswordAsync(TUser user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        user.PasswordHash = null;
        user.RotateSecurityStamp();
        await repository.UpdateAsync(user, cancellationToken);
        return IdentityResult.Success;
    }

    /// <summary>Whether <paramref name="password"/> matches; a match hashed with outdated parameters is re-hashed and persisted.</summary>
    /// <param name="user">The user.</param>
    /// <param name="password">The password to check.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <remarks>Counts no failures and ignores lockout; the sign-in slice owns both.</remarks>
    public async Task<bool> CheckPasswordAsync(TUser user, string password, CancellationToken cancellationToken = default)
    {
        var result = Verify(user, password);
        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = hasher.HashPassword(user, password);
            await repository.UpdateAsync(user, cancellationToken);
        }

        return result != PasswordVerificationResult.Failed;
    }

    /// <summary>Issue a password-reset token; the next stored password voids it.</summary>
    /// <param name="user">The user resetting the password.</param>
    /// <exception cref="NotSupportedException">Purpose tokens are not registered.</exception>
    public string IssuePasswordResetToken(TUser user)
        => RequireTokens().Issue(user, UserTokenPurposeConstants.PasswordReset);

    /// <summary>Replace the password when <paramref name="token"/> is a live reset token for <paramref name="user"/>.</summary>
    /// <param name="user">The user.</param>
    /// <param name="token">The reset token the user received.</param>
    /// <param name="newPassword">The replacement password.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="NotSupportedException">Purpose tokens are not registered.</exception>
    public Task<IdentityResult> ResetPasswordAsync(TUser user, string token, string newPassword, CancellationToken cancellationToken = default)
        => RequireTokens().Verify(user, UserTokenPurposeConstants.PasswordReset, token)
            ? StoreAsync(user, newPassword, cancellationToken)
            : Task.FromResult(IdentityUserExtensions.Failure(IdentityErrorCodeConstants.InvalidToken, "The reset link is invalid or has expired."));

    private PasswordVerificationResult Verify(TUser user, string password)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(password);

        return string.IsNullOrEmpty(user.PasswordHash)
            ? PasswordVerificationResult.Failed
            : hasher.VerifyHashedPassword(user, user.PasswordHash, password);
    }

    private async Task<IdentityResult> StoreAsync(TUser user, string password, CancellationToken cancellationToken)
    {
        var validation = await ValidateAsync(user, password, cancellationToken);
        if (!validation.Succeeded)
            return validation;

        user.PasswordHash = hasher.HashPassword(user, password);
        user.RotateSecurityStamp();
        await repository.UpdateAsync(user, cancellationToken);
        return IdentityResult.Success;
    }

    private UserTokenIssuer<TUser, TKey> RequireTokens()
        => tokens ?? throw new NotSupportedException("Password reset needs purpose tokens; call .AddUserTokens(...) on the identity builder.");
}
