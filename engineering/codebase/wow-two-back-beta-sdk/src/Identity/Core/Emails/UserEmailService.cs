using WoW.Two.Sdk.Backend.Beta.Foundation.Naming;
using WoW.Two.Sdk.Backend.Beta.Identity.Core.Tokens;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Core.Emails;

/// <summary>
/// Provides the email slice: confirmation of the current address and a token-verified move to a new one. Confirmation
/// tokens are scoped to the address they were issued for; change tokens to the new address.
/// </summary>
/// <typeparam name="TUser">The user entity.</typeparam>
/// <typeparam name="TKey">The primary-key type.</typeparam>
/// <param name="repository">The core user store.</param>
/// <param name="accounts">The core account service, for the uniqueness rule.</param>
/// <param name="tokens">The purpose-token issuer.</param>
public sealed class UserEmailService<TUser, TKey>(
    IUserRepository<TUser, TKey> repository,
    UserAccountService<TUser, TKey> accounts,
    UserTokenIssuer<TUser, TKey> tokens)
    where TUser : IdentityUser<TKey>
    where TKey : notnull, IEquatable<TKey>
{
    /// <summary>Issue a token confirming the user's current email address.</summary>
    /// <param name="user">The user.</param>
    public string IssueConfirmationToken(TUser user)
    {
        ArgumentNullException.ThrowIfNull(user);
        return tokens.Issue(user, Scope(UserTokenPurposeConstants.EmailConfirmation, user.NormalizedEmail));
    }

    /// <summary>Mark the email confirmed when <paramref name="token"/> was issued for the current address.</summary>
    /// <param name="user">The user.</param>
    /// <param name="token">The confirmation token.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<IdentityResult> ConfirmEmailAsync(TUser user, string token, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (!tokens.Verify(user, Scope(UserTokenPurposeConstants.EmailConfirmation, user.NormalizedEmail), token))
            return InvalidToken();

        user.EmailConfirmed = true;
        await repository.UpdateAsync(user, cancellationToken);
        return IdentityResult.Success;
    }

    /// <summary>Issue a token authorizing the move to <paramref name="newEmail"/>; send it to that address.</summary>
    /// <param name="user">The user.</param>
    /// <param name="newEmail">The requested address.</param>
    public string IssueChangeEmailToken(TUser user, string newEmail)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentException.ThrowIfNullOrWhiteSpace(newEmail);
        return tokens.Issue(user, Scope(UserTokenPurposeConstants.ChangeEmail, newEmail.ToCanonical()));
    }

    /// <summary>Move to <paramref name="newEmail"/> and mark it confirmed; rotates the security stamp.</summary>
    /// <param name="user">The user.</param>
    /// <param name="newEmail">The requested address the token was issued for.</param>
    /// <param name="token">The change token.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<IdentityResult> ChangeEmailAsync(TUser user, string newEmail, string token, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentException.ThrowIfNullOrWhiteSpace(newEmail);

        var normalized = newEmail.ToCanonical();
        if (!tokens.Verify(user, Scope(UserTokenPurposeConstants.ChangeEmail, normalized), token))
            return InvalidToken();
        if (await accounts.IsEmailTakenAsync(normalized, user, cancellationToken))
            return IdentityUserExtensions.Failure(IdentityErrorCodeConstants.DuplicateEmail, $"Email '{newEmail}' is already taken.");

        user.Email = newEmail;
        user.NormalizedEmail = normalized;
        user.EmailConfirmed = true;
        user.RotateSecurityStamp();
        await repository.UpdateAsync(user, cancellationToken);
        return IdentityResult.Success;
    }

    /// <summary>Set an unconfirmed address directly (administration, sign-up correction); rotates the security stamp.</summary>
    /// <param name="user">The user.</param>
    /// <param name="email">The new address, or null to clear it.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<IdentityResult> SetEmailAsync(TUser user, string? email, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);

        var normalized = email.ToCanonical();
        if (await accounts.IsEmailTakenAsync(normalized, user, cancellationToken))
            return IdentityUserExtensions.Failure(IdentityErrorCodeConstants.DuplicateEmail, $"Email '{email}' is already taken.");

        user.Email = email;
        user.NormalizedEmail = normalized;
        user.EmailConfirmed = false;
        user.RotateSecurityStamp();
        await repository.UpdateAsync(user, cancellationToken);
        return IdentityResult.Success;
    }

    private static string Scope(string purpose, string? value)
        => string.Concat(purpose, UserTokenPurposeConstants.ScopeSeparator.ToString(), value ?? string.Empty);

    private static IdentityResult InvalidToken()
        => IdentityUserExtensions.Failure(IdentityErrorCodeConstants.InvalidToken, "The link is invalid or has expired.");
}
