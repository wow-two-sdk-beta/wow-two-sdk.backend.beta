using System.Globalization;
using WoW.Two.Sdk.Backend.Beta.Identity.Otp;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Core.Phones;

/// <summary>
/// Provides the phone slice over the shipped <see cref="IOtpService"/>: set a number, confirm it with a one-time code
/// and sign in by code. The service creates codes; the host forwards them to its delivery handler (SMS, Telegram, …).
/// </summary>
/// <typeparam name="TUser">The user entity.</typeparam>
/// <typeparam name="TKey">The primary-key type.</typeparam>
/// <param name="repository">The core user store.</param>
/// <param name="otp">Creates and verifies one-time codes.</param>
public sealed class UserPhoneService<TUser, TKey>(IUserRepository<TUser, TKey> repository, IOtpService otp)
    where TUser : IdentityUser<TKey>
    where TKey : notnull, IEquatable<TKey>
{
    /// <summary>The OTP scope of confirmation codes.</summary>
    public const string ConfirmationScope = "identity.phone-confirmation";

    /// <summary>The OTP scope of sign-in codes.</summary>
    public const string SignInScope = "identity.phone-sign-in";

    /// <summary>Set (or clear) the phone number as unconfirmed; rotates the security stamp.</summary>
    /// <param name="user">The user.</param>
    /// <param name="phoneNumber">The number in E.164, or null to clear it.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<IdentityResult> SetPhoneNumberAsync(TUser user, string? phoneNumber, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        user.PhoneNumber = string.IsNullOrWhiteSpace(phoneNumber) ? null : phoneNumber.Trim();
        user.PhoneNumberConfirmed = false;
        user.RotateSecurityStamp();
        await repository.UpdateAsync(user, cancellationToken);
        return IdentityResult.Success;
    }

    /// <summary>Create a code confirming the user's current number; deliver <see cref="OtpCreationResult.Code"/> to it.</summary>
    /// <param name="user">The user.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">The user has no phone number.</exception>
    public Task<OtpCreationResult> CreateConfirmationCodeAsync(TUser user, CancellationToken cancellationToken = default)
        => otp.CreateAsync(ConfirmationSubject(user), ConfirmationScope, cancellationToken);

    /// <summary>Mark the number confirmed when <paramref name="code"/> verifies for the user's current number.</summary>
    /// <param name="user">The user.</param>
    /// <param name="code">The code the user received.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<IdentityResult> ConfirmPhoneNumberAsync(TUser user, string code, CancellationToken cancellationToken = default)
    {
        var verification = await otp.VerifyAsync(ConfirmationSubject(user), code, ConfirmationScope, cancellationToken);
        if (!verification.Success)
            return IdentityUserExtensions.Failure(IdentityErrorCodeConstants.InvalidPhoneCode, "The code is invalid or has expired.");

        user.PhoneNumberConfirmed = true;
        await repository.UpdateAsync(user, cancellationToken);
        return IdentityResult.Success;
    }

    /// <summary>
    /// Create a sign-in code for <paramref name="phoneNumber"/>; null when no account holds it confirmed. Answer the caller
    /// the same way either way, and deliver only a non-null result, so the endpoint reveals no accounts.
    /// </summary>
    /// <param name="phoneNumber">The number in E.164.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<OtpCreationResult?> CreateSignInCodeAsync(string phoneNumber, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(phoneNumber);
        return await repository.FindByConfirmedPhoneNumberAsync(phoneNumber.Trim(), cancellationToken) is null
            ? null
            : await otp.CreateAsync(phoneNumber.Trim(), SignInScope, cancellationToken);
    }

    /// <summary>The user a verified sign-in code belongs to, or null; complete the sign-in with <c>SignInService.SignInAsync</c>.</summary>
    /// <param name="phoneNumber">The number in E.164.</param>
    /// <param name="code">The code the user received.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<TUser?> VerifySignInCodeAsync(string phoneNumber, string code, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(phoneNumber);
        var verification = await otp.VerifyAsync(phoneNumber.Trim(), code, SignInScope, cancellationToken);
        return verification.Success
            ? await repository.FindByConfirmedPhoneNumberAsync(phoneNumber.Trim(), cancellationToken)
            : null;
    }

    private static string ConfirmationSubject(TUser user)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (string.IsNullOrEmpty(user.PhoneNumber))
            throw new InvalidOperationException("The user has no phone number to confirm.");

        return string.Concat(Convert.ToString(user.Id, CultureInfo.InvariantCulture), ":", user.PhoneNumber);
    }
}
