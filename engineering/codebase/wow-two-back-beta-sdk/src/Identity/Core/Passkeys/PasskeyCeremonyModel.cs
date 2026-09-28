namespace WoW.Two.Sdk.Backend.Beta.Identity.Core.Passkeys;

/// <summary>Represents a started WebAuthn ceremony: options for the browser and the sealed state to send back.</summary>
public sealed record PasskeyCeremonyModel
{
    /// <summary>Gets the options JSON for <c>navigator.credentials.create</c> or <c>.get</c>.</summary>
    public required string OptionsJson { get; init; }

    /// <summary>Gets the opaque state the client returns with the credential; it expires with the ceremony.</summary>
    public required string State { get; init; }
}
