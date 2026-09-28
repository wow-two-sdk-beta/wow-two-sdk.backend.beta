namespace WoW.Two.Sdk.Backend.Beta.Identity.Core.Endpoints;

/// <summary>Chooses the two-factor method a sign-in asks for first.</summary>
public sealed record TwoFactorMethodApiRequest
{
    /// <summary>An available method, such as <c>authenticator</c> or <c>sms</c>.</summary>
    public required string Method { get; init; }
}
