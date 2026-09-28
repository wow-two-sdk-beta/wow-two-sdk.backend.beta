namespace WoW.Two.Sdk.Backend.Beta.Identity.Core.RefreshTokens;

/// <summary>Carries a redemption's status, and on success the user and the replacement token.</summary>
/// <typeparam name="TUser">The user entity.</typeparam>
public sealed record RefreshTokenResult<TUser>
    where TUser : class
{
    /// <summary>The outcome.</summary>
    public required RefreshTokenStatus Status { get; init; }

    /// <summary>The token's user; set on success. Complete with <c>SignInService.SignInAsync</c> to apply lockout and preconditions.</summary>
    public TUser? User { get; init; }

    /// <summary>The replacement token; set on success. The redeemed token is spent.</summary>
    public RefreshTokenModel? Token { get; init; }

    /// <summary>Whether the redemption succeeded.</summary>
    public bool Succeeded => Status == RefreshTokenStatus.Succeeded;
}
