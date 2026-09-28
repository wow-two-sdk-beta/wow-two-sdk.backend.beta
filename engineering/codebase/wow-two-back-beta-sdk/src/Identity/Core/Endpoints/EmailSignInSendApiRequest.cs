namespace WoW.Two.Sdk.Backend.Beta.Identity.Core.Endpoints;

/// <summary>Asks for a sign-in link or code by email; always answered 204, so no account is revealed.</summary>
public sealed record EmailSignInSendApiRequest
{
    /// <summary>The address.</summary>
    public required string Email { get; init; }

    /// <summary><c>link</c> (default) or <c>code</c>.</summary>
    public string? Method { get; init; }
}
