namespace WoW.Two.Sdk.Backend.Beta.Identity.Core;

/// <summary>Holds the stable <see cref="IdentityError.Code"/> values the identity slices return.</summary>
public static class IdentityErrorCodeConstants
{
    /// <summary>A user was created without a user name.</summary>
    public const string UserNameRequired = "UserNameRequired";

    /// <summary>Another account holds the normalized user name.</summary>
    public const string DuplicateUserName = "DuplicateUserName";

    /// <summary>Another account holds the normalized email.</summary>
    public const string DuplicateEmail = "DuplicateEmail";

    /// <summary>The password is shorter than the configured minimum.</summary>
    public const string PasswordTooShort = "PasswordTooShort";

    /// <summary>The password is longer than the configured maximum.</summary>
    public const string PasswordTooLong = "PasswordTooLong";

    /// <summary>The password lacks a required digit.</summary>
    public const string PasswordRequiresDigit = "PasswordRequiresDigit";

    /// <summary>The password lacks a required lowercase letter.</summary>
    public const string PasswordRequiresLower = "PasswordRequiresLower";

    /// <summary>The password lacks a required uppercase letter.</summary>
    public const string PasswordRequiresUpper = "PasswordRequiresUpper";

    /// <summary>The password lacks a required non-alphanumeric character.</summary>
    public const string PasswordRequiresNonAlphanumeric = "PasswordRequiresNonAlphanumeric";

    /// <summary>The password has fewer distinct characters than required.</summary>
    public const string PasswordRequiresUniqueChars = "PasswordRequiresUniqueChars";

    /// <summary>The password equals the account's user name or email.</summary>
    public const string PasswordMatchesAccount = "PasswordMatchesAccount";

    /// <summary>The password appears in a known breach corpus.</summary>
    public const string PasswordBreached = "PasswordBreached";

    /// <summary>The breach corpus could not be queried and the check fails closed.</summary>
    public const string PasswordBreachCheckUnavailable = "PasswordBreachCheckUnavailable";

    /// <summary>The supplied current password does not match.</summary>
    public const string PasswordMismatch = "PasswordMismatch";

    /// <summary>A password was added to an account that already has one.</summary>
    public const string UserAlreadyHasPassword = "UserAlreadyHasPassword";

    /// <summary>A purpose token is malformed, expired, for another purpose or user, or voided by a stamp rotation.</summary>
    public const string InvalidToken = "InvalidToken";

    /// <summary>The account is locked out.</summary>
    public const string LockedOut = "LockedOut";

    /// <summary>Lockout is disabled for the account.</summary>
    public const string LockoutNotEnabled = "LockoutNotEnabled";

    /// <summary>Another role holds the normalized role name.</summary>
    public const string DuplicateRoleName = "DuplicateRoleName";

    /// <summary>A role was created without a name.</summary>
    public const string RoleNameRequired = "RoleNameRequired";

    /// <summary>No role carries the requested name.</summary>
    public const string RoleNotFound = "RoleNotFound";

    /// <summary>The user already holds the role.</summary>
    public const string UserAlreadyInRole = "UserAlreadyInRole";

    /// <summary>The user does not hold the role.</summary>
    public const string UserNotInRole = "UserNotInRole";

    /// <summary>The external login already belongs to an account.</summary>
    public const string LoginAlreadyAssociated = "LoginAlreadyAssociated";

    /// <summary>The authenticator code does not verify against the user's key.</summary>
    public const string InvalidAuthenticatorCode = "InvalidAuthenticatorCode";

    /// <summary>The recovery code is unknown or already redeemed.</summary>
    public const string InvalidRecoveryCode = "InvalidRecoveryCode";

    /// <summary>A one-time code sent to the phone does not verify.</summary>
    public const string InvalidPhoneCode = "InvalidPhoneCode";

    /// <summary>The login or password is wrong, or the account does not exist.</summary>
    public const string SignInFailed = "SignInFailed";

    /// <summary>The credentials were right but the email or phone is not confirmed yet.</summary>
    public const string SignInNotAllowed = "SignInNotAllowed";

    /// <summary>The password was right; the request must repeat with a second-factor code.</summary>
    public const string TwoFactorRequired = "TwoFactorRequired";

    /// <summary>The refresh token is unknown, spent, expired or revoked.</summary>
    public const string InvalidRefreshToken = "InvalidRefreshToken";

    /// <summary>Two-factor was enabled before an authenticator key was verified.</summary>
    public const string AuthenticatorNotConfigured = "AuthenticatorNotConfigured";

    /// <summary>The two-factor method is not configured, or the account has no confirmed address for it.</summary>
    public const string TwoFactorMethodUnavailable = "TwoFactorMethodUnavailable";

    /// <summary>The delivered two-factor code is wrong, spent or expired.</summary>
    public const string InvalidTwoFactorCode = "InvalidTwoFactorCode";
}
