namespace WoW.Two.Sdk.Backend.Beta.Identity.Core.Tokens;

/// <summary>Holds the purposes the identity slices bind their tokens to.</summary>
public static class UserTokenPurposeConstants
{
    /// <summary>Confirms the account's current email address.</summary>
    public const string EmailConfirmation = "EmailConfirmation";

    /// <summary>Authorizes one password reset.</summary>
    public const string PasswordReset = "PasswordReset";

    /// <summary>Authorizes a move to one new email address.</summary>
    public const string ChangeEmail = "ChangeEmail";

    /// <summary>Separates a purpose from the value it is scoped to, as in <c>ChangeEmail:{normalized address}</c>.</summary>
    public const char ScopeSeparator = ':';
}
