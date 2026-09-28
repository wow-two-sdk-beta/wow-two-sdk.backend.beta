using System.Globalization;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Backend.Beta.Identity.Otp;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Core.EmailSignIn;

/// <summary>
/// Provides passwordless sign-in by email over <see cref="IOtpService"/>: a single-use link token (12 characters, long
/// lived) or a short numeric code. Unknown addresses get nothing and learn nothing; a used token or code is spent.
/// Complete the sign-in with <c>SignInService.SignInAsync</c>, which applies lockout, preconditions and two-factor.
/// </summary>
/// <typeparam name="TUser">The user entity.</typeparam>
/// <typeparam name="TKey">The primary-key type.</typeparam>
/// <param name="accounts">Finds users by normalized email.</param>
/// <param name="users">Saves an address confirmed by use.</param>
/// <param name="otp">Creates and spends the tokens and codes.</param>
/// <param name="options">Lifetimes and confirmation; <c>Identity:EmailSignIn</c> reloads live.</param>
public sealed class UserEmailSignInService<TUser, TKey>(
    UserAccountService<TUser, TKey> accounts,
    IUserRepository<TUser, TKey> users,
    IOtpService otp,
    IOptionsMonitor<EmailSignInOptions> options)
    where TUser : IdentityUser<TKey>
    where TKey : notnull, IEquatable<TKey>
{
    /// <summary>The OTP scope of sign-in links.</summary>
    public const string LinkScope = "identity.email-sign-in-link";

    /// <summary>The OTP scope of sign-in codes.</summary>
    public const string CodeScope = "identity.email-sign-in-code";

    /// <summary>
    /// Creates a link token for the account holding <paramref name="email"/>; null when there is none or one was sent
    /// moments ago. Answer the caller the same way either way, so the endpoint reveals no accounts.
    /// </summary>
    /// <param name="email">The address.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<(TUser User, OtpCreationResult Token)?> CreateLinkTokenAsync(string email, CancellationToken cancellationToken = default)
        => CreateAsync(email, LinkScope, new OtpCodeSpec { Kind = OtpCodeKind.Alphanumeric, Length = 12, Lifetime = options.CurrentValue.LinkLifetime }, cancellationToken);

    /// <summary>Creates a numeric code for the account holding <paramref name="email"/>; null as for links.</summary>
    /// <param name="email">The address.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<(TUser User, OtpCreationResult Code)?> CreateCodeAsync(string email, CancellationToken cancellationToken = default)
    {
        var current = options.CurrentValue;
        return CreateAsync(email, CodeScope, new OtpCodeSpec { Kind = OtpCodeKind.Numeric, Length = current.CodeLength, Lifetime = current.CodeLifetime }, cancellationToken);
    }

    /// <summary>The user a link token belongs to, spending it; null when it is wrong, spent or expired.</summary>
    /// <param name="email">The address the link was sent to.</param>
    /// <param name="token">The token from the link.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<TUser?> VerifyLinkTokenAsync(string email, string token, CancellationToken cancellationToken = default)
        => VerifyAsync(email, token, LinkScope, cancellationToken);

    /// <summary>The user a code belongs to, spending it; null when it is wrong, spent or expired.</summary>
    /// <param name="email">The address the code was sent to.</param>
    /// <param name="code">The code.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<TUser?> VerifyCodeAsync(string email, string code, CancellationToken cancellationToken = default)
        => VerifyAsync(email, code, CodeScope, cancellationToken);

    private async Task<(TUser, OtpCreationResult)?> CreateAsync(string email, string scope, OtpCodeSpec spec, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        if (await accounts.FindByEmailAsync(email.Trim(), cancellationToken) is not { } user)
            return null;

        var created = await otp.CreateAsync(Subject(user), scope, spec, cancellationToken);
        return created.Success ? (user, created) : null;
    }

    private async Task<TUser?> VerifyAsync(string email, string secret, string scope, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(secret)
            || await accounts.FindByEmailAsync(email.Trim(), cancellationToken) is not { } user
            || !(await otp.VerifyAsync(Subject(user), secret.Trim(), scope, cancellationToken)).Success)
        {
            return null;
        }

        if (options.CurrentValue.ConfirmEmail && !user.EmailConfirmed)
        {
            user.EmailConfirmed = true;
            await users.UpdateAsync(user, cancellationToken);
        }

        return user;
    }

    private static string Subject(TUser user) => "user:" + Convert.ToString(user.Id, CultureInfo.InvariantCulture);
}
