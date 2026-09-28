namespace WoW.Two.Sdk.Backend.Beta.Identity.Core.SignIn;

/// <summary>The outcomes of a sign-in attempt.</summary>
public enum SignInStatus
{
    /// <summary>The credentials were wrong, or the account does not exist.</summary>
    Failed,

    /// <summary>The user is signed in; the result carries the principal.</summary>
    Succeeded,

    /// <summary>The account is inside a lockout window.</summary>
    LockedOut,

    /// <summary>The credentials were right but a precondition (confirmed email or phone) is unmet.</summary>
    NotAllowed,

    /// <summary>The password was right; a second factor must complete the sign-in.</summary>
    RequiresTwoFactor,
}
